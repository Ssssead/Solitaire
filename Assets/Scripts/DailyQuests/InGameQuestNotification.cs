using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class InGameQuestNotification : MonoBehaviour
{
    [Header("UI References (Шаблон)")]
    public RectTransform panelRect;
    public CanvasGroup canvasGroup;
    public Image backgroundImage;
    public TMP_Text titleText;
    public Image progressBarFill;
    public TMP_Text progressText;

    [Header("Multi-Panel Settings")]
    public float verticalSpacing = 130f;
    public float cascadeDelay = 0.5f;

    [Header("Animation Settings")]
    public float slideDuration = 0.25f;
    public float fillDelay = 0.5f;
    public float fillDuration = 0.5f;
    public float showDuration = 2.0f;
    public float debounceTime = 0.8f;
    public float fillPitchOffset = 0.6f;

    [Header("Complete Animation (Успех)")]
    public Color defaultBgColor = new Color32(154, 95, 64, 255);
    public Color completeBgColor = new Color32(194, 122, 78, 255);
    public float pulseScale = 1.25f;
    public float pulseDuration = 0.5f;

    [Header("Difficulty Styling")]
    public Color easyColor = new Color(0.5f, 0.8f, 0.2f);
    public Color mediumColor = new Color(1f, 0.7f, 0f);
    public Color hardColor = new Color(0.9f, 0.2f, 0.2f);

    public Sprite easySprite;
    public Sprite mediumSprite;
    public Sprite hardSprite;

    private Vector2 baseVisiblePosition;
    private float hideOffset = 400f;

    private Dictionary<string, PendingNotification> pendingNotifications = new Dictionary<string, PendingNotification>();
    private Queue<PendingNotification> displayQueue = new Queue<PendingNotification>();

    private Dictionary<string, ActivePanelState> activePanels = new Dictionary<string, ActivePanelState>();

    private bool isDisplaying = false;
    private List<bool> occupiedSlots = new List<bool>();
    public static InGameQuestNotification Instance { get; private set; }
    private int _cancelToken = 0;

    private class PendingNotification
    {
        public QuestInstance Quest;
        public int OldProgress;
        public int NewProgress;
        public float Timer;
    }

    private class ActivePanelState
    {
        public int TargetProgress;
        public bool IsCompleted;
    }

    private void Awake()
    {
        Instance = this;

        if (panelRect != null)
        {
            baseVisiblePosition = panelRect.anchoredPosition;
            panelRect.anchoredPosition = new Vector2(baseVisiblePosition.x - hideOffset, baseVisiblePosition.y);
            panelRect.gameObject.SetActive(false);
        }
    }

    public void ForceCloseAll()
    {
        _cancelToken++;
        displayQueue.Clear();
        pendingNotifications.Clear();
    }

    private void OnEnable() { QuestManager.OnQuestProgressNotification += HandleQuestProgress; }
    private void OnDisable() { QuestManager.OnQuestProgressNotification -= HandleQuestProgress; }

    private void Update()
    {
        if (pendingNotifications.Count == 0) return;

        List<string> readyKeys = null;

        foreach (var kvp in pendingNotifications)
        {
            kvp.Value.Timer -= Time.deltaTime;

            if (kvp.Value.Timer <= 0f)
            {
                if (readyKeys == null) readyKeys = new List<string>();
                readyKeys.Add(kvp.Key);
            }
        }

        if (readyKeys != null)
        {
            List<PendingNotification> batch = new List<PendingNotification>();
            foreach (var key in readyKeys)
            {
                batch.Add(pendingNotifications[key]);
                pendingNotifications.Remove(key);
            }

            batch.Sort((a, b) => ((int)a.Quest.template.category).CompareTo((int)b.Quest.template.category));

            foreach (var item in batch) displayQueue.Enqueue(item);

            if (!isDisplaying) StartCoroutine(ProcessDisplayQueue());
        }
    }

    private void HandleQuestProgress(QuestInstance quest, int oldProg, int newProg)
    {
        if (PlayerPrefs.GetInt("QuestNotificationsEnabled", 1) == 0) return;

        if (activePanels.TryGetValue(quest.questId, out var activeState))
        {
            activeState.TargetProgress = newProg;
            activeState.IsCompleted = quest.isCompleted;
            return;
        }

        foreach (var item in displayQueue)
        {
            if (item.Quest.questId == quest.questId)
            {
                item.NewProgress = newProg;
                item.Quest = quest;
                return;
            }
        }

        if (pendingNotifications.TryGetValue(quest.questId, out var pending))
        {
            pending.NewProgress = newProg;
            pending.Timer = debounceTime;
        }
        else
        {
            pendingNotifications[quest.questId] = new PendingNotification
            {
                Quest = quest,
                OldProgress = oldProg,
                NewProgress = newProg,
                Timer = debounceTime
            };
        }
    }

    private IEnumerator ProcessDisplayQueue()
    {
        isDisplaying = true;
        int myToken = _cancelToken;

        while (displayQueue.Count > 0)
        {
            if (myToken != _cancelToken) break;

            var data = displayQueue.Dequeue();
            StartCoroutine(SpawnAndAnimatePanel(data));
            yield return new WaitForSeconds(cascadeDelay);
        }

        isDisplaying = false;
    }

    private int GetFreeSlot()
    {
        for (int i = 0; i < occupiedSlots.Count; i++)
        {
            if (!occupiedSlots[i]) { occupiedSlots[i] = true; return i; }
        }
        occupiedSlots.Add(true);
        return occupiedSlots.Count - 1;
    }

    private void FreeSlot(int index)
    {
        if (index >= 0 && index < occupiedSlots.Count) occupiedSlots[index] = false;
    }

    private T GetEquivalent<T>(T templateComp, GameObject clone) where T : Component
    {
        if (templateComp == null) return null;
        if (templateComp.gameObject == panelRect.gameObject) return clone.GetComponent<T>();

        T[] all = clone.GetComponentsInChildren<T>(true);
        foreach (var c in all)
        {
            if (c.gameObject.name == templateComp.gameObject.name) return c;
        }
        return null;
    }

    private void UpdatePanelVisuals(Image fillImage, TMP_Text textComp, float currentProgress, int targetValue)
    {
        if (fillImage != null) fillImage.fillAmount = currentProgress / targetValue;
        if (textComp != null) textComp.text = $"{Mathf.RoundToInt(currentProgress)}/{targetValue}";
    }

    private IEnumerator SpawnAndAnimatePanel(PendingNotification data)
    {
        int myToken = _cancelToken;

        int slot = GetFreeSlot();
        GameObject clone = Instantiate(panelRect.gameObject, panelRect.parent);
        clone.SetActive(true);

        var duplicateScript = clone.GetComponent<InGameQuestNotification>();
        if (duplicateScript != null) Destroy(duplicateScript);

        RectTransform cRect = clone.GetComponent<RectTransform>();
        CanvasGroup cGroup = clone.GetComponent<CanvasGroup>();
        if (cGroup == null && canvasGroup != null) cGroup = clone.AddComponent<CanvasGroup>();

        bool isPortrait = Screen.height > Screen.width;
        float currentScale = isPortrait ? 2f : 1f;
        cRect.localScale = new Vector3(currentScale, currentScale, 1f);

        TMP_Text cTitle = GetEquivalent(titleText, clone);
        Image cFill = GetEquivalent(progressBarFill, clone);
        TMP_Text cProgText = GetEquivalent(progressText, clone);
        Image cBg = GetEquivalent(backgroundImage, clone);

        if (cBg != null) cBg.color = defaultBgColor;

        string titleKey = data.Quest.template.questId + "_Title";
        string localizedTitle = LocalizationManager.instance != null ? LocalizationManager.instance.GetLocalizedValue(titleKey) : data.Quest.template.questId;
        if (string.IsNullOrEmpty(localizedTitle) || localizedTitle.Contains("not found"))
            localizedTitle = data.Quest.template.questId;

        if (cTitle != null) cTitle.text = localizedTitle;

        QuestDifficulty difficulty = data.Quest.template.difficulty;
        Color targetColor = Color.white;
        Sprite targetSprite = null;

        switch (difficulty)
        {
            case QuestDifficulty.Easy: targetColor = easyColor; targetSprite = easySprite; break;
            case QuestDifficulty.Medium: targetColor = mediumColor; targetSprite = mediumSprite; break;
            case QuestDifficulty.Hard: targetColor = hardColor; targetSprite = hardSprite; break;
        }

        if (cTitle != null) cTitle.color = targetColor;
        if (cFill != null && targetSprite != null) cFill.sprite = targetSprite;
        if (cProgText != null) cProgText.color = Color.white;

        ActivePanelState state = new ActivePanelState
        {
            TargetProgress = data.NewProgress,
            IsCompleted = data.Quest.isCompleted
        };
        activePanels[data.Quest.questId] = state;

        float currentVisualProgress = data.OldProgress;
        UpdatePanelVisuals(cFill, cProgText, currentVisualProgress, data.Quest.targetValue);

        // ---> ИЗМЕНЕНИЕ: Компенсируем Pivot, чтобы левый край не уезжал за экран <---
        float pivotOffsetX = cRect.rect.width * cRect.pivot.x * (currentScale - 1f);

        Vector2 targetVisible = new Vector2(baseVisiblePosition.x + pivotOffsetX, baseVisiblePosition.y - (verticalSpacing * currentScale * slot));
        Vector2 targetHidden = new Vector2(targetVisible.x - (hideOffset * currentScale), targetVisible.y);
        // -------------------------------------------------------------------------

        if (myToken != _cancelToken) { FreeSlot(slot); Destroy(clone); yield break; }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Panel_Slide_In");

        // Передаем флаг isShowing = true, чтобы скрипт не полагался на сломанные координаты для прозрачности
        yield return SlideTo(cRect, cGroup, targetHidden, targetVisible, slideDuration, true);

        if (fillDelay > 0f) yield return new WaitForSeconds(fillDelay);

        bool hasPulsed = false;

        while (true)
        {
            if (myToken != _cancelToken) break;

            if (currentVisualProgress < state.TargetProgress)
            {
                float startFillVal = currentVisualProgress;
                float endFillVal = state.TargetProgress;

                AudioSource xpSound = null;
                float startPitch = 1f;
                if (AudioManager.Instance != null)
                {
                    xpSound = AudioManager.Instance.PlaySound("XP_Gain");
                    if (xpSound != null) startPitch = xpSound.pitch;
                }

                float elapsed = 0f;
                while (elapsed < fillDuration)
                {
                    if (myToken != _cancelToken) break;
                    elapsed += Time.deltaTime;
                    float t = elapsed / fillDuration;
                    float easeT = t * t * t;

                    if (xpSound != null) xpSound.pitch = Mathf.Lerp(startPitch, startPitch + fillPitchOffset, easeT);

                    currentVisualProgress = Mathf.Lerp(startFillVal, endFillVal, easeT);
                    UpdatePanelVisuals(cFill, cProgText, currentVisualProgress, data.Quest.targetValue);

                    yield return null;
                }

                currentVisualProgress = endFillVal;
                UpdatePanelVisuals(cFill, cProgText, currentVisualProgress, data.Quest.targetValue);

                if (xpSound != null && xpSound.isPlaying) xpSound.Stop();

                if (state.IsCompleted && currentVisualProgress >= data.Quest.targetValue && !hasPulsed)
                {
                    if (myToken == _cancelToken)
                    {
                        hasPulsed = true;
                        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Level_Up");
                        yield return StartCoroutine(CompletePulseAnimation(cRect, cBg, cProgText));
                    }
                }
            }

            if (myToken != _cancelToken) break;

            float currentShowTimer = showDuration;
            while (currentShowTimer > 0)
            {
                if (myToken != _cancelToken) break;
                if (currentVisualProgress < state.TargetProgress) break;

                currentShowTimer -= Time.deltaTime;
                yield return null;
            }

            if (myToken != _cancelToken || (currentVisualProgress >= state.TargetProgress && currentShowTimer <= 0f))
            {
                break;
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySound("Panel_Slide_Out");

        // Передаем флаг isShowing = false
        yield return SlideTo(cRect, cGroup, targetVisible, targetHidden, slideDuration, false);

        activePanels.Remove(data.Quest.questId);
        FreeSlot(slot);
        Destroy(clone);
    }

    private IEnumerator SlideTo(RectTransform rt, CanvasGroup cg, Vector2 startPos, Vector2 targetPos, float duration, bool isShowing)
    {
        if (rt == null) yield break;

        float startAlpha = cg != null ? cg.alpha : (isShowing ? 0f : 1f);
        float targetAlpha = isShowing ? 1f : 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / duration), 3f);

            if (rt != null) rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            if (cg != null) cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            yield return null;
        }

        if (rt != null) rt.anchoredPosition = targetPos;
        if (cg != null) cg.alpha = targetAlpha;
    }

    private IEnumerator CompletePulseAnimation(RectTransform rt, Image bg, TMP_Text progText)
    {
        if (rt == null) yield break;

        Vector3 startScale = rt.localScale;
        Quaternion startRot = Quaternion.identity;

        if (progText != null) progText.color = new Color32(255, 187, 0, 255);

        float dur1 = pulseDuration * 0.2f;
        float dur2 = pulseDuration * 0.3f;
        float dur3 = pulseDuration * 0.3f;
        float dur4 = pulseDuration * 0.2f;

        float t = 0;
        while (t < dur1)
        {
            t += Time.deltaTime;
            float norm = t / dur1;
            rt.localScale = Vector3.Lerp(startScale, startScale * 0.9f, norm);
            rt.localRotation = Quaternion.Lerp(startRot, Quaternion.Euler(0, 0, 2f), norm);
            yield return null;
        }

        if (bg != null) bg.color = Color.white;

        t = 0;
        while (t < dur2)
        {
            t += Time.deltaTime;
            float norm = t / dur2;
            float ease = 1f - Mathf.Pow(1f - norm, 3f);

            rt.localScale = Vector3.Lerp(startScale * 0.9f, startScale * pulseScale, ease);
            rt.localRotation = Quaternion.Lerp(Quaternion.Euler(0, 0, 2f), Quaternion.Euler(0, 0, -3f), ease);

            if (bg != null) bg.color = Color.Lerp(Color.white, completeBgColor, norm);
            yield return null;
        }

        t = 0;
        while (t < dur3)
        {
            t += Time.deltaTime;
            float norm = t / dur3;
            float ease = norm * norm * (3f - 2f * norm);

            rt.localScale = Vector3.Lerp(startScale * pulseScale, startScale * 0.95f, ease);
            rt.localRotation = Quaternion.Lerp(Quaternion.Euler(0, 0, -3f), Quaternion.Euler(0, 0, 1f), ease);
            yield return null;
        }

        t = 0;
        while (t < dur4)
        {
            t += Time.deltaTime;
            float norm = t / dur4;

            rt.localScale = Vector3.Lerp(startScale * 0.95f, startScale, norm);
            rt.localRotation = Quaternion.Lerp(Quaternion.Euler(0, 0, 1f), startRot, norm);
            yield return null;
        }

        rt.localScale = startScale;
        rt.localRotation = startRot;
        if (bg != null) bg.color = completeBgColor;
    }
}