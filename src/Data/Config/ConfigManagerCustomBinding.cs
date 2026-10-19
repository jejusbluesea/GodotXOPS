using System;
using System.Collections.Generic;

namespace GodotXOPS
{
    // ConfigManager 의 커스텀 바인딩 담당 partial: 유저가 처음 키와 다르게 바꾼 커스텀 바인딩의 키를 이름으로 든다 (config.json 의 customBindings).
    // 등록한 맵팩이 지금 없어도 값은 남긴다. 같은 이름은 맵팩이 달라도 같은 값을 쓴다.
    public partial class ConfigManager
    {
        // 마지막 저장 시점의 customBindings (옵션 BACK 으로 되돌리기).
        private readonly List<CustomBindingValue> m_savedCustomBindings = new List<CustomBindingValue>();

        /// <summary>
        /// 유저가 바꿔 둔 커스텀 바인딩의 키를 찾는다.
        /// </summary>
        /// <param name="name">액션 이름 (대소문자 무시).</param>
        /// <param name="path">바인딩 경로. 빈 문자열이면 키를 뗀 것이다.</param>
        /// <returns>바꿔 둔 값이 있으면 true. 없으면 등록 파일의 처음 키를 쓴다.</returns>
        public bool GetCustomBinding(string name, out string path)
        {
            foreach (CustomBindingValue value in Config.customBindings)
            {
                if (string.Equals(value.name, name, StringComparison.OrdinalIgnoreCase))
                {
                    path = value.binding ?? string.Empty;
                    return true;
                }
            }
            path = string.Empty;
            return false;
        }

        /// <summary>
        /// 커스텀 바인딩의 키를 바꾼다 (SAVE 로 파일에 남는다). 처음 키와 같으면 남긴 값을 지운다.
        /// </summary>
        /// <param name="name">액션 이름.</param>
        /// <param name="path">새 바인딩 경로.</param>
        /// <param name="baseValue">등록 파일의 처음 키.</param>
        public void SetCustomBinding(string name, string path, string baseValue)
        {
            Config.customBindings.RemoveAll(value => string.Equals(value.name, name, StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(path ?? string.Empty, baseValue ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                Config.customBindings.Add(new CustomBindingValue { name = name, binding = path ?? string.Empty });
            }
        }

        /// <summary>
        /// 지금의 customBindings 를 저장된 상태로 기록한다.
        /// </summary>
        private void SnapshotCustomBindings()
        {
            m_savedCustomBindings.Clear();
            foreach (CustomBindingValue value in Config.customBindings)
            {
                m_savedCustomBindings.Add(new CustomBindingValue { name = value.name, binding = value.binding });
            }
        }

        /// <summary>
        /// customBindings 를 마지막 저장 상태로 되돌린다. 라이브 액션은 부르는 쪽이 다시 맞춘다 (RestoreBindings).
        /// </summary>
        private void RevertCustomBindings()
        {
            Config.customBindings.Clear();
            foreach (CustomBindingValue value in m_savedCustomBindings)
            {
                Config.customBindings.Add(new CustomBindingValue { name = value.name, binding = value.binding });
            }
        }
    }
}
