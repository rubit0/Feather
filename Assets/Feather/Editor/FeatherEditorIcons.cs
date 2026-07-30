using UnityEditor;
using UnityEngine;

namespace Feather.Editor
{
    internal static class FeatherEditorIcons
    {
        private const string JsScriptIconPath = "Assets/Feather/Editor/Icons/js Script Icon.png";

        private static Texture2D _source;
        private static Texture2D _uncompressed;

        /// <summary>
        /// Uncompressed RGBA32 icon for ScriptedImporter thumbnails / static preview.
        /// Compressed GUI textures cannot be passed to EncodeTo* (import worker error).
        /// </summary>
        public static Texture2D JsScriptIcon
        {
            get
            {
                if (_uncompressed != null) return _uncompressed;

                _source = AssetDatabase.LoadAssetAtPath<Texture2D>(JsScriptIconPath);
                if (_source == null)
                    _source = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;

                _uncompressed = CreateUncompressedCopy(_source);
                return _uncompressed;
            }
        }

        /// <summary>Fresh RGBA32 copy sized for <see cref="Editor.RenderStaticPreview"/>.</summary>
        public static Texture2D CreateStaticPreview(int width, int height)
        {
            var src = JsScriptIcon;
            if (src == null) return null;

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (src.width == width && src.height == height && IsUncompressed(src))
            {
                EditorUtility.CopySerialized(src, tex);
                return tex;
            }

            var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        private static Texture2D CreateUncompressedCopy(Texture2D source)
        {
            if (source == null) return null;
            if (IsUncompressed(source) && source.isReadable)
                return source;

            var w = source.width;
            var h = source.height;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "js Script Icon",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            copy.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return copy;
        }

        private static bool IsUncompressed(Texture2D tex)
        {
            var f = tex.format;
            return f == TextureFormat.RGBA32
                   || f == TextureFormat.ARGB32
                   || f == TextureFormat.RGB24
                   || f == TextureFormat.Alpha8
                   || f == TextureFormat.RGBAFloat
                   || f == TextureFormat.RGBAHalf;
        }
    }
}
