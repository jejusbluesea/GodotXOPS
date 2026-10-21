// 하늘 위에 겹치는 풍경(도시, 산) 셰이더. 하늘과 같은 모델을 카메라 위치에 둔 것처럼 그리고, 텍스처의 알파로 하늘과 섞는다.
// 하늘(sky_mesh)과 해·달(sky_sun) 다음에 그려진다 (렌더 우선순위). 깊이를 읽지도 쓰지도 않고 안개는 받지 않는다.
shader_type spatial;
render_mode unshaded, blend_mix, cull_back, depth_draw_never, depth_test_disabled, fog_disabled, skip_vertex_transform;

// source_color 를 붙이지 않는다 — 하늘과 같이 sRGB 값에 밝기를 곱한 뒤 마지막에만 선형으로 바꾼다.
uniform sampler2D main_texture : hint_default_transparent, filter_linear, repeat_enable;

global uniform float xops_model_brightness;
// 날씨가 하늘을 가릴 때(hideSky)의 색(rgb, sRGB)과 가리는 정도(a). 0 이면 가리지 않는다.
global uniform vec4 xops_sky_cover;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	// 하늘과 같은 보정: 원본 XOPS 기준으로 Y축 180도 회전 (X, Z 반전), 카메라 회전만 적용, 먼 면에 붙인다.
	vec3 direction = vec3(-VERTEX.x, VERTEX.y, -VERTEX.z);
	VERTEX = (VIEW_MATRIX * vec4(direction, 0.0)).xyz;
	POSITION = PROJECTION_MATRIX * vec4(VERTEX, 1.0);
	POSITION.z = 0.0;
}

void fragment() {
	vec4 color = texture(main_texture, UV);
	ALBEDO = srgb_to_linear(mix(color.rgb * xops_model_brightness, xops_sky_cover.rgb, xops_sky_cover.a));
	ALPHA = color.a;
}
