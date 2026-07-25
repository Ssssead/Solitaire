using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems; // Обязательно добавляем это для работы с курсором
#if TMP_YG2
using TMPro;
#endif

namespace YG
{
    // Добавляем интерфейсы IPointerEnterHandler и IPointerExitHandler
    public class LBPlayerDataYG : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ImageLoadYG imageLoad;

        [Serializable]
        public struct TextLegasy
        {
            public Text rank, name, score;
        }
        public TextLegasy textLegasy;

#if TMP_YG2
        [Serializable]
        public struct TextMP
        {
            public TextMeshProUGUI rank, name, score;
        }
        public TextMP textMP;
#endif

        [Header("Компонент фона")]
        public Image backgroundImage;

        [Header("Цвета фонов")]
        public Color color1stPlace = Color.white;
        public Color color2ndPlace = Color.white;
        public Color color3rdPlace = Color.white;
        public Color colorCurrentPlayer = Color.white;
        public Color colorNormal = Color.white;

        [Header("Настройки цветов текста")]
        public Color darkTextColor = new Color(0.21f, 0.12f, 0.06f);
        public Color lightTextColor = Color.white;
        public Color scoreAccentColor = new Color(0.96f, 0.72f, 0.13f);

        [Header("Анимация при наведении (Hover)")]
        public float hoverScale = 1.03f; // Насколько увеличивается (1.03 = на 3%)
        public float scaleSpeed = 15f;   // Скорость анимации (чем больше, тем резче)

        private Vector3 targetScale;
        private Vector3 defaultScale;

        public class Data
        {
            public string rank;
            public string name;
            public string score;
            public string photoUrl;
            public bool inTop;
            public bool currentPlayer;
            public Sprite photoSprite;
        }

        [HideInInspector]
        public Data data = new Data();

        private void Start()
        {
            // Запоминаем оригинальный масштаб (обычно это 1,1,1)
            defaultScale = transform.localScale;
            targetScale = defaultScale;
        }

        private void Update()
        {
            // Плавно интерполируем масштаб к целевому значению каждый кадр
            if (transform.localScale != targetScale)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, targetScale, Time.deltaTime * scaleSpeed);
            }
        }

        // Вызывается, когда курсор мыши заходит на объект
        public void OnPointerEnter(PointerEventData eventData)
        {
            targetScale = defaultScale * hoverScale;

            // Если нужно, чтобы увеличенная плашка была поверх остальных, раскомментируйте строку ниже:
            // transform.SetAsLastSibling(); 
        }

        // Вызывается, когда курсор мыши уходит с объекта
        public void OnPointerExit(PointerEventData eventData)
        {
            targetScale = defaultScale;
        }

        public void UpdateEntries()
        {
            if (textLegasy.rank && data.rank != null) textLegasy.rank.text = data.rank;
            if (textLegasy.name && data.name != null) textLegasy.name.text = data.name;
            if (textLegasy.score && data.score != null) textLegasy.score.text = data.score;

#if TMP_YG2
            if (textMP.rank && data.rank != null) textMP.rank.text = data.rank;
            if (textMP.name && data.name != null) textMP.name.text = data.name;
            if (textMP.score && data.score != null) textMP.score.text = data.score;
#endif
            if (imageLoad)
            {
                if (data.photoSprite) imageLoad.SetTexture(data.photoSprite.texture);
                else if (data.photoUrl == null) imageLoad.ClearTexture();
                else imageLoad.Load(data.photoUrl);
            }

            if (backgroundImage != null)
            {
                if (data.currentPlayer)
                {
                    backgroundImage.color = colorCurrentPlayer;
                    SetTextColors(darkTextColor, darkTextColor, darkTextColor);
                }
                else if (data.rank == "1")
                {
                    backgroundImage.color = color1stPlace;
                    SetTextColors(darkTextColor, darkTextColor, darkTextColor);
                }
                else if (data.rank == "2")
                {
                    backgroundImage.color = color2ndPlace;
                    SetTextColors(darkTextColor, darkTextColor, darkTextColor);
                }
                else if (data.rank == "3")
                {
                    backgroundImage.color = color3rdPlace;
                    SetTextColors(lightTextColor, lightTextColor, lightTextColor); // Поставил везде светлый, как вы сделали на скриншоте
                }
                else
                {
                    backgroundImage.color = colorNormal;
                    SetTextColors(lightTextColor, lightTextColor, lightTextColor);
                }
            }
        }

        private void SetTextColors(Color rankColor, Color nameColor, Color scoreColor)
        {
            if (textLegasy.rank) textLegasy.rank.color = rankColor;
            if (textLegasy.name) textLegasy.name.color = nameColor;
            if (textLegasy.score) textLegasy.score.color = scoreColor;

#if TMP_YG2
            if (textMP.rank) textMP.rank.color = rankColor;
            if (textMP.name) textMP.name.color = nameColor;
            if (textMP.score) textMP.score.color = scoreColor;
#endif
        }
    }
}