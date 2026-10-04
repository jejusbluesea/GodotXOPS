using System.Collections.Generic;
using System.IO;
using Godot;

namespace GodotXOPS.IO
{
    /// <summary>
    /// 이미지 파일(BMP, TGA, DDS, JPG, PNG)을 런타임에 읽어 텍스처로 만들고 캐시하는 로더.
    /// 디코딩은 Godot 내장 로더에 맡긴다.
    /// </summary>
    public static class ImageLoader
    {
        private const int k_maxTextureSize = 4096;

        private static readonly Dictionary<string, ImageTexture> s_textureCache = new Dictionary<string, ImageTexture>();

        /// <summary>
        /// 지정된 경로의 이미지 파일을 로드해 텍스처로 반환한다. 이미 로드된 텍스처는 캐시에서 반환한다.
        /// 필터링은 Godot 에서 텍스처가 아닌 머티리얼/셰이더 샘플러가 정하므로 여기서 받지 않는다.
        /// </summary>
        /// <param name="filepath">이미지 파일 전체 경로.</param>
        /// <returns>로드된 텍스처. 실패 시 null.</returns>
        public static ImageTexture LoadTexture(string filepath)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                Debugger.LogError("Image path is empty.", nameof(ImageLoader));
                return null;
            }

            if (s_textureCache.TryGetValue(filepath, out ImageTexture cached))
            {
                return cached;
            }

            Image image = LoadImage(filepath);
            if (image == null)
            {
                return null;
            }

            ImageTexture texture = ImageTexture.CreateFromImage(image);
            texture.ResourceName = Path.GetFileNameWithoutExtension(filepath);
            s_textureCache.Add(filepath, texture);
            return texture;
        }

        /// <summary>
        /// 지정된 경로의 이미지 파일을 디코딩해 RGBA8 + 밉맵 이미지로 반환한다. 캐시하지 않는다.
        /// </summary>
        /// <param name="filepath">이미지 파일 전체 경로.</param>
        /// <returns>디코딩된 이미지. 실패 시 null.</returns>
        public static Image LoadImage(string filepath)
        {
            if (!File.Exists(filepath))
            {
                Debugger.LogError($"Image file not found: {filepath}", nameof(ImageLoader));
                return null;
            }

            byte[] bytes = File.ReadAllBytes(filepath);
            var image = new Image();
            Error error;
            switch (Path.GetExtension(filepath).ToLowerInvariant())
            {
                case ".bmp":
                    error = image.LoadBmpFromBuffer(bytes);
                    break;
                case ".tga":
                    error = image.LoadTgaFromBuffer(bytes);
                    break;
                case ".dds":
                    error = image.LoadDdsFromBuffer(bytes);
                    break;
                case ".png":
                    error = image.LoadPngFromBuffer(bytes);
                    break;
                case ".jpg":
                case ".jpeg":
                    error = image.LoadJpgFromBuffer(bytes);
                    break;
                default:
                    Debugger.LogError($"Unsupported image extension: {filepath}", nameof(ImageLoader));
                    return null;
            }

            if (error != Error.Ok || image.IsEmpty())
            {
                Debugger.LogError($"Failed to decode image ({error}): {filepath}", nameof(ImageLoader));
                return null;
            }

            // DDS 는 DXT 압축 상태로 올 수 있다. 크기 조절·밉맵 생성·픽셀 접근을 위해 항상 RGBA8 로 푼다.
            if (image.IsCompressed() && image.Decompress() != Error.Ok)
            {
                Debugger.LogError($"Failed to decompress image: {filepath}", nameof(ImageLoader));
                return null;
            }
            image.Convert(Image.Format.Rgba8);

            int width = image.GetWidth();
            int height = image.GetHeight();
            if (width > k_maxTextureSize || height > k_maxTextureSize)
            {
                float scale = (float)k_maxTextureSize / Mathf.Max(width, height);
                image.Resize(
                    Mathf.Max(1, Mathf.FloorToInt(width * scale)),
                    Mathf.Max(1, Mathf.FloorToInt(height * scale)),
                    Image.Interpolation.Bilinear);
            }

            image.GenerateMipmaps();
            return image;
        }

        /// <summary>
        /// 텍스처 캐시를 비운다.
        /// </summary>
        public static void ClearCache()
        {
            s_textureCache.Clear();
        }
    }
}
