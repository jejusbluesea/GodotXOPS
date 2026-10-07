using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 블록 텍스처 목록 파일(JSON)의 내용을 담는 컨테이너 클래스. BD2 파일이 이 파일의 경로를 들고 있고, 여러 맵이 한 파일을 같이 쓸 수 있다.
    /// </summary>
    public class BlockTextureListData
    {
        public List<BlockTextureData> blockTextureData = new List<BlockTextureData>();
    }
}
