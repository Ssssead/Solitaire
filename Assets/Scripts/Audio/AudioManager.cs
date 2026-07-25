using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;

        [Range(0.1f, 3f)] public float basePitch = 1f;
        [Range(0f, 0.5f)] public float pitchRandomness = 0.1f;
        public float cooldown = 0.05f;

        // <--- НОВЫЕ НАСТРОЙКИ КОМБО --->
        [Header("Combo Settings")]
        [Tooltip("Включить нарастание высоты звука при быстрых повторениях?")]
        public bool isComboSound = false;

        [Tooltip("На сколько увеличивать Pitch при каждом следующем звуке")]
        public float comboPitchStep = 0.05f;

        [Tooltip("Максимальный предел Pitch (чтобы звук не стал писком)")]
        [Range(1f, 3f)] public float maxComboPitch = 2.0f;

        [Tooltip("Время в секундах, после которого комбо сбрасывается")]
        public float comboResetTime = 1.2f;
    }

    [Header("Звуки")]
    public Sound[] sounds;

    [Header("Настройки пула")]
    [Tooltip("Сколько звуков максимум может звучать одновременно")]
    public int audioSourceCount = 10;

    [Header("Состояние")]
    public bool isMuted = false;

    private List<AudioSource> audioSources;
    private Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>();
    private Dictionary<string, int> comboCounts = new Dictionary<string, int>();
    // Словарь для хранения активных корутин затухания, чтобы прерывать их при необходимости
    private Dictionary<AudioSource, Coroutine> activeFades = new Dictionary<AudioSource, Coroutine>();

    private void Awake()
    {
        // Настройка Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Загружаем сохраненную настройку (по умолчанию 0 - звук включен)
            isMuted = PlayerPrefs.GetInt("IsMuted", 0) == 1;

            InitializePool();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePool()
    {
        audioSources = new List<AudioSource>();
        for (int i = 0; i < audioSourceCount; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSources.Add(source);
        }
    }

    /// <summary>
    /// Включает или выключает все звуки в игре и сохраняет настройку.
    /// </summary>
    public void SetMute(bool muteState)
    {
        if (isMuted == muteState) return; // Если состояние не изменилось, ничего не делаем

        isMuted = muteState;

        // Сохраняем настройку в память устройства
        PlayerPrefs.SetInt("IsMuted", isMuted ? 1 : 0);
        PlayerPrefs.Save();

        // Если выключили звук, резко глушим всё, что сейчас играет
        if (isMuted)
        {
            foreach (var source in audioSources)
            {
                source.Stop();
            }
        }
    }

    /// <summary>
    /// Базовый метод проигрывания звука. Возвращает AudioSource, если нужно с ним работать.
    /// </summary>
    public AudioSource PlaySound(string soundName)
    {
        if (isMuted) return null;

        Sound s = System.Array.Find(sounds, item => item.name == soundName);
        if (s == null) return null;

        // --- ЛОГИКА КОМБО (Проверяем время с прошлого запуска) ---
        if (s.isComboSound)
        {
            if (lastPlayTimes.TryGetValue(soundName, out float lastTime))
            {
                // Если с прошлого звука прошло меньше времени, чем comboResetTime -> увеличиваем комбо
                if (Time.time - lastTime <= s.comboResetTime)
                {
                    comboCounts[soundName] = comboCounts.ContainsKey(soundName) ? comboCounts[soundName] + 1 : 1;
                }
                else
                {
                    // Иначе сбрасываем
                    comboCounts[soundName] = 0;
                }
            }
            else
            {
                comboCounts[soundName] = 0;
            }
        }

        // Проверка кулдауна
        if (lastPlayTimes.TryGetValue(soundName, out float lastTimeCooldown))
        {
            if (Time.time - lastTimeCooldown < s.cooldown) return null;
        }

        // ВАЖНО: Записываем время текущего запуска
        lastPlayTimes[soundName] = Time.time;

        AudioSource availableSource = audioSources.Find(source => !source.isPlaying);
        if (availableSource == null) availableSource = audioSources[0];

        if (activeFades.ContainsKey(availableSource) && activeFades[availableSource] != null)
        {
            StopCoroutine(activeFades[availableSource]);
        }

        availableSource.clip = s.clip;
        availableSource.volume = s.volume;

        // --- РАСЧЕТ ФИНАЛЬНОГО PITCH С УЧЕТОМ КОМБО ---
        float finalPitch = s.basePitch + Random.Range(-s.pitchRandomness, s.pitchRandomness);

        if (s.isComboSound && comboCounts.ContainsKey(soundName))
        {
            // Прибавляем шаг комбо за каждый успешный раз
            finalPitch += comboCounts[soundName] * s.comboPitchStep;

            // Ограничиваем максимальным значением
            finalPitch = Mathf.Min(finalPitch, s.maxComboPitch);
        }

        availableSource.pitch = finalPitch;
        availableSource.Play();

        return availableSource;
    }

    /// <summary>
    /// Проигрывает звук, а затем плавно уводит его громкость в ноль. Идеально для скольжения панелей и полета карт.
    /// </summary>
    /// <param name="soundName">Имя звука из массива</param>
    /// <param name="playDuration">Сколько секунд звук играет на полной громкости</param>
    /// <param name="fadeDuration">За сколько секунд звук должен плавно затихнуть</param>
    public void PlaySoundWithAutoFade(string soundName, float playDuration, float fadeDuration = 0.15f)
    {
        // Проверяем, есть ли такой звук вообще
        Sound s = System.Array.Find(sounds, item => item.name == soundName);
        if (s == null) return;

        // Запускаем звук стандартным методом
        AudioSource source = PlaySound(soundName);

        if (source != null)
        {
            // Запускаем корутину затухания и запоминаем её
            Coroutine fadeRoutine = StartCoroutine(FadeOutRoutine(source, playDuration, fadeDuration, s.volume));
            activeFades[source] = fadeRoutine;
        }
    }

    private IEnumerator FadeOutRoutine(AudioSource source, float delayBeforeFade, float fadeDuration, float originalVolume)
    {
        // Ждем, пока основная часть анимации закончится
        yield return new WaitForSeconds(delayBeforeFade);

        float startVolume = source.volume;
        float elapsed = 0f;

        // Плавно убавляем громкость
        while (elapsed < fadeDuration)
        {
            // Важно: если звук внезапно остановили (например, выключили звук в настройках), выходим
            if (!source.isPlaying) yield break;

            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, elapsed / fadeDuration);
            yield return null;
        }

        // Выключаем звук полностью и возвращаем настройки для следующего использования
        source.Stop();
        source.volume = originalVolume;
        activeFades[source] = null;
    }
}