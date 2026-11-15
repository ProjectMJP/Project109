using System.IO;
using UnityEngine;

public static class ImageLoader
{
    /// <summary>
    /// 로컬 경로에 있는 이미지 파일(.png, .jpg)을 읽어들여 런타임에 Sprite로 변환합니다.
    /// 최대 크기를 초과하면 강제로 줄이지는 않지만, 모더에게 최적화 경고 로그를 남깁니다.
    /// </summary>
    /// <param name="absolutePath">이미지 파일의 절대 경로</param>
    /// <param name="maxSize">경고 기준이 되는 최대 가로/세로 픽셀 크기 (0이면 검사 안함)</param>
    /// <returns>생성된 Sprite 객체 (실패 시 null)</returns>
    public static Sprite LoadCustomSprite(string absolutePath, int maxSize = 0)
    {
        if (!File.Exists(absolutePath)) return null;

        // 1. 이미지를 바이트 배열로 읽음
        byte[] fileData = File.ReadAllBytes(absolutePath);
        
        // 2. 임시 텍스처 생성 (크기는 LoadImage 시점에 자동으로 맞춰짐)
        Texture2D texture = new Texture2D(2, 2);
        
        // 3. 바이트 데이터를 이미지 포맷에 맞춰 자동 디코딩
        if (texture.LoadImage(fileData)) 
        {
            // 리사이징 연산(Graphics.Blit, ReadPixels)은 CPU/GPU 동기화로 인한 로딩 지연(Spike)이 심하므로 제거.
            // 대신 모더가 최적화하도록 경고 로그만 남깁니다.
            if (maxSize > 0 && (texture.width > maxSize || texture.height > maxSize))
            {
                Debug.LogWarning($"[ImageLoader] 이미지 '{Path.GetFileName(absolutePath)}' 의 크기가 너무 큽니다 ({texture.width}x{texture.height}). 메모리와 로딩 성능 최적화를 위해 가급적 {maxSize}px 이하로 줄여주세요.");
            }

            // 4. UI에 바로 띄울 수 있도록 Sprite 객체로 포장해서 반환
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
        
        return null;
    }
}
