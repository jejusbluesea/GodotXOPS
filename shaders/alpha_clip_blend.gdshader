// 블록·소물·사람·무기 공용 셰이더. 원본 XOPS 의 고정 파이프라인을 재현한다:
// 조명 없음, 텍스처 × 정점색(면 명도), 알파 테스트 + 알파 블렌딩, 깊이 쓰기, 선형 안개.
// 원본은 감마 보정 없이 색을 그대로 곱하고 섞으므로, 계산을 sRGB 값 그대로 한 뒤 마지막에만 선형으로 바꾼다.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_always, cull_back, fog_disabled;

// source_color 를 붙이지 않는다 — sRGB 값을 그대로 받아 원본과 같은 공간에서 계산하기 위해서다.
uniform sampler2D main_texture : hint_default_white, filter_linear_mipmap, repeat_enable;
uniform vec4 tint = vec4(1.0);
uniform float cutoff : hint_range(0.001, 0.1) = 0.004;

// MapLoader 가 스카이에 맞춰 설정한다. rgb = 안개 색(sRGB 값), a = 안개 사용 여부(0 또는 1).
global uniform vec4 xops_fog_color;
// x = 안개 시작 거리, y = 안개 끝 거리 (미터, 카메라 정면 방향 깊이).
global uniform vec2 xops_fog_range;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 col = texture(main_texture, UV) * COLOR * tint;
	if (col.a < cutoff) {
		discard;
	}

	float depth = -VERTEX.z;
	float fog = clamp((depth - xops_fog_range.x) / max(xops_fog_range.y - xops_fog_range.x, 0.0001), 0.0, 1.0) * xops_fog_color.a;
	col.rgb = mix(col.rgb, xops_fog_color.rgb, fog);

	ALBEDO = srgb_to_linear(clamp(col.rgb, 0.0, 1.0));
	ALPHA = col.a;
}
