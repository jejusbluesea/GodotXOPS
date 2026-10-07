namespace GodotXOPS
{
    /// <summary>
    /// 블록 텍스처 목록의 항목 하나. BD2 의 면 텍스처 번호가 이 항목의 목록 번호를 가리킨다.
    /// 경로 문자열이 아니라 객체로 둔 것은 나중에 같은 항목에 다른 텍스처(노멀 등)를 더할 수 있게 하기 위해서다.
    /// </summary>
    public class BlockTextureData
    {
        // exe 폴더 기준 색 텍스처 경로. 비어 있으면 원본의 빈 텍스처 슬롯처럼 흰 면이 된다.
        public string diffusePath = string.Empty;
    }
}
