using System.IO;
using LostBreadcrumbs.Runtime.Core.Input;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace LostBreadcrumbs.Runtime.UI
{
    public static class CampaignStoryCopy
    {
        public const string PrologueAndStage1 =
            "숲은 깊고 집은 멀었다. 헨젤이 주머니를 열어 빵가루를 뿌리며 말했다. \"이 길을 따라가면 돌아갈 수 있어.\"";

        public const string Stage2 =
            "숲이 속삭였다. 달콤한 냄새는 길을 잃게 만드는 함정이었다. 그레텔이 연기를 피워 그림자를 가렸다.";

        public const string Stage3 =
            "심장이 빠르게 뛰었다. 고요한 순간이 올 때까지 숨을 죽이고, 멀리까지 귀를 기울여야 했다.";

        public const string ContinueHint = "스페이스로 계속";
        public const string TitleLogo = "헨젤과 그레텔";
        public const string StartLabel = "시작";
    }

    public static class CampaignArt
    {
        private const string FrameResourcePath = "Story/StorybookOpenParchmentFrame";
        private const string Stage1ResourcePath = "Story/StorybookHanselGretelForestPath";
        private const string FrameFileName = "StorybookOpenParchmentFrame.png";
        private const string Stage1FileName = "StorybookHanselGretelForestPath.png";

        private static Sprite bookFrame;
        private static Sprite stage1Illustration;

        public static Sprite TryGetBookFrame()
        {
            if (bookFrame != null)
            {
                return bookFrame;
            }

            bookFrame = LoadSprite(FrameResourcePath, FrameFileName, "StorybookOpenParchmentFrame");
            return bookFrame;
        }

        public static Sprite TryGetStage1Illustration()
        {
            if (stage1Illustration != null)
            {
                return stage1Illustration;
            }

            stage1Illustration = LoadSprite(Stage1ResourcePath, Stage1FileName, "StorybookHanselGretelForestPath");
            return stage1Illustration;
        }

        private static Sprite LoadSprite(string resourcePath, string fileName, string spriteName)
        {
            Sprite resourceSprite = Resources.Load<Sprite>(resourcePath);
            if (resourceSprite != null)
            {
                return resourceSprite;
            }

            Texture2D texture = LoadTextureFromProjectFile(fileName);
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
            }

            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Texture2D LoadTextureFromProjectFile(string fileName)
        {
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "_Project/Resources/Story", fileName),
                Path.Combine(Application.dataPath, "_Project/Art", fileName)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(fileName),
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (texture.LoadImage(bytes, markNonReadable: false))
                {
                    return texture;
                }

                Object.Destroy(texture);
            }

            return null;
        }
    }

    public static class CampaignUiInput
    {
        public static bool ConfirmPressed()
        {
            return RuntimeInputAdapter.GetKeyDown(KeyCode.Space) || PrimaryClickDown();
        }

        public static bool SkipPressed()
        {
            return RuntimeInputAdapter.GetKeyDown(KeyCode.Escape);
        }

        public static bool PrimaryClickDown()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }
}
