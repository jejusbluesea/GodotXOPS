// 이펙트(총구 화염, 연기, 탄피, 혈흔) 빌보드용 셰이더. 원본 XOPS 의 이펙트 그리기를 재현한다:
// 조명 없음, 텍스처 × 알파, 알파 블렌딩(가산 아님), 깊이 쓰기 없음, 깊이 테스트는 유지(벽 뒤는 가려진다), 양면, 선형 안개.
// alpha_clip_blend 와 같이 sRGB 값 그대로 계산한 뒤 마지막에만 선형으로 바꾼다.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_disabled, fog_disabled;

// source_color 를 붙이지 않는다 — sRGB 값을 그대로 받아 원본과 같은 공간에서 계산하기 위해서다.
uniform sampler2D main_texture : hint_default_white, filter_linear_mipmap, repeat_disable;
// 이펙트 하나하나의 투명도. 같은 머티리얼을 공유하면서 노드마다 다른 값을 준다.
instance uniform float effect_alpha = 1.0;

global uniform vec4 xops_fog_color;
global uniform vec2 xops_fog_range;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 col = texture(main_texture, UV);

	float depth = -VERTEX.z;
	float fog = clamp((depth - xops_fog_range.x) / max(xops_fog_range.y - xops_fog_range.x, 0.0001), 0.0, 1.0) * xops_fog_color.a;
	col.rgb = mix(col.rgb, xops_fog_color.rgb, fog);

	ALBEDO = srgb_to_linear(clamp(col.rgb, 0.0, 1.0));
	ALPHA = col.a * clamp(effect_alpha, 0.0, 1.0);
}
