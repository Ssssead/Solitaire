using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LongPressHoldTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("Настройки")]
    public float holdDuration = 1.0f;

    [Tooltip("Ссылка на полупрозрачный Image (Fill Method: Horizontal)")]
    public Image fillIndicator;

    [Header("Событие при долгом нажатии")]
    // Теперь это поле будет видно в Инспекторе!
    public UnityEvent onLongPress;

    private Button button;
    private bool isPointerDown = false;
    private float pointerDownTimer = 0f;
    private bool triggered = false;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void Update()
    {
        if (isPointerDown && !triggered)
        {
            pointerDownTimer += Time.deltaTime;

            if (fillIndicator != null)
            {
                fillIndicator.fillAmount = pointerDownTimer / holdDuration;
            }

            if (pointerDownTimer >= holdDuration)
            {
                triggered = true;
                onLongPress?.Invoke();

                if (fillIndicator != null)
                    fillIndicator.fillAmount = 0f;
            }
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (button != null && !button.interactable) return;

        isPointerDown = true;
        pointerDownTimer = 0f;
        triggered = false;
    }

    public void OnPointerUp(PointerEventData eventData) => ResetState();
    public void OnPointerExit(PointerEventData eventData) => ResetState();

    private void ResetState()
    {
        isPointerDown = false;
        pointerDownTimer = 0f;
        triggered = false;
        if (fillIndicator != null) fillIndicator.fillAmount = 0f;
    }

    // Метод для подписки из кода (для UndoManager)
    public static void SubscribeToButton(Button btn, UnityAction action)
    {
        if (btn == null) return;

        LongPressHoldTrigger trigger = btn.GetComponent<LongPressHoldTrigger>();
        if (trigger == null)
        {
            trigger = btn.gameObject.AddComponent<LongPressHoldTrigger>();
            trigger.holdDuration = 1.0f;
        }

        trigger.onLongPress.AddListener(action);
    }
}