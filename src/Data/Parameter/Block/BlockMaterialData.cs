using System.Collections.Generic;

namespace GodotXOPS
{
    /// <summary>
    /// 블록 면의 재질 하나. 밟았을 때의 발소리와 총알이 맞았을 때의 이펙트·소리를 정한다.
    /// BD2 의 면 재질 번호가 이 항목의 목록 번호를 가리킨다. 재질 번호가 없는 BD1 의 면은 전부 0번을 쓰고, 0번에는 원본의 벽 착탄 연기와 착탄음이 들어 있다.
    /// </summary>
    public class BlockMaterialData
    {
        public string name = string.Empty;
        // 발소리 WAV 경로 목록. 목록에서 하나를 무작위로 골라 재생한다. 비어 있으면 소리가 나지 않는다.
        public List<string> footstepWalk = new List<string>();
        public List<string> footstepRun = new List<string>();
        public List<string> footstepLanding = new List<string>();
        // 총알이 맞았을 때의 WAV 경로 목록.
        public List<string> hitSounds = new List<string>();
        // 총알이 맞았을 때의 이펙트 프리셋 번호. 0(NONE)이면 내지 않는다.
        public int hitEffect;
        // 총알이 맞은 자리에 남기는 탄흔 이펙트 프리셋 번호. 크기에 탄환의 bulletHoleSize 가 곱해진다. 0(NONE)이면 남기지 않는다.
        public int bulletHoleEffect;
    }
}
