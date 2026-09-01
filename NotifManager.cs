using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DiscordNotifs
{
    public class NotifManager : MonoBehaviour
    {
        public static NotifManager Instance { get; private set; }

        private GameObject vrCanvasObj;
        private GameObject desktopCanvasObj;
        private Text vrText;
        private Text desktopText;
        private Image vrIcon;
        private Image desktopIcon;
        private Coroutine hideCoroutine;

        private const string discordLogoBase64 = "iVBORw0KGgoAAAANSUhEUgAAAQAAAAEACAMAAABrrFhUAAAAY1BMVEVYZfJib/OMlfahqPeWn/eBi/XLz/vq6/7////19f5tePTq7P22vPnV2Pyrsvirsvl3gvT09f7Axfp3gfRtePNsePPg4v22vPq2u/qCi/WhqPjf4/zf4v2Xn/essvjLzvuXnvdbidFTAAAETElEQVR4AezBgQAAAACAoP2pF6kCAAAAAAAAAAAAAAAAAAAAAIDZudMtV1UlDuCFRKlWIEJ6uOwbzXn/lzzzYc/GWiT6zya/79WrLeYSc5Vq9IFWa3Sr6JehWt0ZZn5RtFJvmHnodPsrPLx1/B9PKx1ziLOPnIRRO84EXaAP/CWnR3pArTWcybpA5G8NsX20pw+cSbpAngEeOQenY+Cf8KIZ4FuDfSV4Ko/7hS7wNjYH7W3MvNeHtn2jvxn+OXcgaP0x8KJo43vgnwqu85EXDfGVULWON9G1BOmDN/M/AnTgDSWC0xve0KAITeSsykFw4qzOQWB4YwNBOfLmPAHpeXsvr5XOgJkjGA3vIlU6A2bvOHvAnXwiCMrwTl5UpUtg5us7BAB2gcg78nXugaC6QORd+bo7AEAXiLwzX+8SANEFNHPdXcAwV90FDgxA037+zwAc7aZlCKnSNTDrADZBdU6DBwbha5wCAabBkWGkSqfAzFa6C8xeADYB9Y2ByEBsbSMAYAy0zHWPActQLPQuKBh3DiwiDRlwzwFOv9JfTpORh5x5rVfQc8CQiLLJiEMaA1oW6XgVq+grVh4yY56JA68x07fm8hCIhXCUPn823zgkG/HK4Rf6kYv8YBt5BQ03BQyv9CMq8M/JQ7IItw+e6cd8QQjKTqCX3OMTtOdCCNZOoCnqkrYgZEFD2/FF/08qDAE4Dji+TtHPKHknVmBboVB2i9HI9zIGahZUhaVqVxCyQEEVQ7rSBMj3QiPUUTCWJkC+8zrQVjzmELBYG2H5jDYUFqAiQDlMtAwKQgjr+nwoq9O2BSEQJQFVWKeNBSEQ6+BYeG3BFIUAHIfasmsLh7IQgLcjDZd0AWXEIZRDMDYCuuj73g95yJGxEuBLPmr6VBSyzMO9Fpzko3kqeA1r8W4GHOWNKQ/JIl4COL4SZf2lPAQhAY4lYrv860rlIVmHlYAsuBhjFwpCwO4LOkb0TMAzAc8EPBPwTMAGngl4JuCZgMig4jMB27AMykJUhCr4ekwzKI10T9hpwzcz6DNSUbRdORzThW/CJSKagd4LjKurof1suFCYVR54MDckpsBXDLk3pliQgxBTHneBrwiaNtOfeUUKCnMQYlKC32x2r7SlmSUpoOQdi5xtoqx1DNP8WW9kKSCVvAu8QnC2USR4/I2bP5vDmhS80pdOjXULw8dc7HSiL6ljYLTmz/ooKvJTdkqTt9G5s/mHczH6qXlV9I32Ehi0+QVfQbn7HryHhvY033V1Tuu3CRncOIj3rL3EV9pf7+53ced0bY+MIZm7ndEt9uNnkxN8OSWhAvjjZ8ktnIoKaMDHF0yH8S416C4Rpv7bU094pWJ9QFv4BJOBvnkFzjWKMvhu4G78IibMIz2EFM3KFUAwCEI+ID9MDia6kd/+enpFj+YE+af+aA8OZAAAAAAG+Vvf46sAAAAAAAAAAAAAAAAAAAAAAFYCeHSjWah9hFcAAAAASUVORK5CYII=";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetupUI()
        {
            desktopCanvasObj = new GameObject("DiscordNotifs_DesktopCanvas");
            DontDestroyOnLoad(desktopCanvasObj);
            var dCanvas = desktopCanvasObj.AddComponent<Canvas>();
            dCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            dCanvas.sortingOrder = 100;
            desktopCanvasObj.AddComponent<CanvasScaler>();
            desktopCanvasObj.AddComponent<GraphicRaycaster>();

            var dPanel = CreatePanel(desktopCanvasObj.transform, true);
            desktopText = dPanel.GetComponentInChildren<Text>();
            desktopIcon = dPanel.transform.Find("Icon").GetComponent<Image>();

            vrCanvasObj = new GameObject("DiscordNotifs_VRCanvas");
            DontDestroyOnLoad(vrCanvasObj);
            var vCanvas = vrCanvasObj.AddComponent<Canvas>();
            vCanvas.renderMode = RenderMode.WorldSpace;
            vCanvas.sortingOrder = 100;

            var vPanel = CreatePanel(vrCanvasObj.transform, false);
            vrText = vPanel.GetComponentInChildren<Text>();
            vrIcon = vPanel.transform.Find("Icon").GetComponent<Image>();

            Hide();
        }

        private void LateUpdate()
        {
            if (vrCanvasObj != null && vrCanvasObj.activeSelf)
            {
                if (GorillaTagger.Instance == null || GorillaTagger.Instance.offlineVRRig == null || GorillaLocomotion.GTPlayer.Instance == null) return;

                bool isRightHand = DiscordNotifsPlugin.HandChoiceConfig?.Value?.Equals("Right", System.StringComparison.OrdinalIgnoreCase) == true;

                var handTransform = isRightHand ? GorillaTagger.Instance.rightHandTransform : GorillaTagger.Instance.leftHandTransform;
                var playerHand = isRightHand ? GorillaLocomotion.GTPlayer.Instance.RightHand : GorillaLocomotion.GTPlayer.Instance.LeftHand;

                Quaternion rot = handTransform.rotation * playerHand.handRotOffset;
                Vector3 pos = handTransform.position + handTransform.rotation * playerHand.handOffset;

                vrCanvasObj.transform.position = pos;
                vrCanvasObj.transform.LookAt(GorillaTagger.Instance.headCollider.transform.position);
                vrCanvasObj.transform.position += vrCanvasObj.transform.forward * 0.1f;
                vrCanvasObj.transform.Rotate(0, 180f, 0);

                vrCanvasObj.transform.localScale = Vector3.one * 0.0010f;
            }
        }

        private GameObject CreatePanel(Transform parent, bool isDesktop)
        {
            var panel = new GameObject("NotificationPanel");
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            
            if (isDesktop)
            {
                rect.anchorMin = new Vector2(1, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(1, 1);
                rect.anchoredPosition = new Vector2(-20, -20);
                rect.sizeDelta = new Vector2(300, 80);
            }
            else
            {
                rect.sizeDelta = new Vector2(300, 80);
            }

            var bg = panel.AddComponent<Image>();
            bg.sprite = GetRoundedRectSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.98f); // Dark background, less seethrough

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(panel.transform, false);
            var iconRect = iconObj.AddComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0, 0.5f);
            iconRect.anchorMax = new Vector2(0, 0.5f);
            iconRect.pivot = new Vector2(0, 0.5f);
            iconRect.anchoredPosition = new Vector2(10, 0);
            iconRect.sizeDelta = new Vector2(40, 40);

            var iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = GetDiscordSprite();

            var textObj = new GameObject("Text");
            textObj.transform.SetParent(panel.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.offsetMin = new Vector2(60, 5);
            textRect.offsetMax = new Vector2(-10, -5);

            var text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.color = Color.white;
            text.fontSize = 16;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return panel;
        }

        private Sprite GetDiscordSprite()
        {
            byte[] imageBytes = System.Convert.FromBase64String(discordLogoBase64);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(imageBytes);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private Sprite GetRoundedRectSprite()
        {
            int radius = 12;
            int size = radius * 2 + 2;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0, x - (size - radius - 1));
                    float dy = Mathf.Max(radius - y, 0, y - (size - radius - 1));
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                    else
                    {
                        pixels[y * size + x] = Color.clear;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        public void QueueNotification(string title, string content, float duration, string imageUrl = null)
        {
            if (hideCoroutine != null)
            {
                StopCoroutine(hideCoroutine);
            }

            string fullText = string.IsNullOrEmpty(title) ? content : $"<b>{title}</b>\n{content}";
            desktopText.text = fullText;
            vrText.text = fullText;

            Canvas.ForceUpdateCanvases();
            float dHeight = Mathf.Max(80f, desktopText.preferredHeight + 20f);
            desktopText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(300, dHeight);
            
            float vHeight = Mathf.Max(80f, vrText.preferredHeight + 20f);
            vrText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(300, vHeight);

            desktopCanvasObj.SetActive(true);
            vrCanvasObj.SetActive(true);

            if (!string.IsNullOrEmpty(imageUrl))
            {
                StartCoroutine(LoadImageRoutine(imageUrl));
            }
            else
            {
                var sprite = GetDiscordSprite();
                desktopIcon.sprite = sprite;
                vrIcon.sprite = sprite;
            }

            hideCoroutine = StartCoroutine(HideAfterDelay(duration));
        }

        private IEnumerator LoadImageRoutine(string url)
        {
            using (UnityEngine.Networking.UnityWebRequest uwr = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
            {
                yield return uwr.SendWebRequest();
#pragma warning disable CS0618
                if (uwr.isNetworkError || uwr.isHttpError)
#pragma warning restore CS0618
                {
                    var sprite = GetDiscordSprite();
                    desktopIcon.sprite = sprite;
                    vrIcon.sprite = sprite;
                }
                else
                {
                    var tex = UnityEngine.Networking.DownloadHandlerTexture.GetContent(uwr);
                    var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    desktopIcon.sprite = sprite;
                    vrIcon.sprite = sprite;
                }
            }
        }

        private IEnumerator HideAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Hide();
        }

        private void Hide()
        {
            if (desktopCanvasObj != null) desktopCanvasObj.SetActive(false);
            if (vrCanvasObj != null) vrCanvasObj.SetActive(false);
        }
    }
}
