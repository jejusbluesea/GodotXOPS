// 해·달 셰이더. 정해진 방향의 하늘에 늘 카메라를 보는 사각형 하나를 그린다 (빌보드). 메시는 한 변이 2 인 사각형(QuadMesh)이다.
// 하늘(sky_mesh) 다음, 풍경(sky_layer) 전에 그려진다 (렌더 우선순위). 깊이를 읽지도 쓰지도 않고 안개는 받지 않는다.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled, fog_disabled, skip_vertex_transform;

uniform sampler2D main_texture : hint_default_transparent, filter_linear, repeat_disable;
// 해가 있는 방향 (월드 공간, 길이 1).
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);
// 거리 1 에서의 사각형 반지름 (= tan(겉보기 크기 / 2)).
uniform float sun_half_size = 0.1;

global uniform float xops_model_brightness;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void vertex() {
	// 카메라 회전만 적용한 해의 방향. 카메라 뒤에 있으면 화면 밖으로 보낸다.
	vec3 center = (VIEW_MATRIX * vec4(sun_direction, 0.0)).xyz;
	if (center.z >= 0.0) {
		POSITION = vec4(2.0, 2.0, 0.0, 1.0);
	} else {
		// 거리 1 의 자리에서 화면과 나란하게 펼친다.
		VERTEX = center + vec3(VERTEX.x, VERTEX.y, 0.0) * sun_half_size;
		POSITION = PROJECTION_MATRIX * vec4(VERTEX, 1.0);
		POSITION.z = 0.0;
	}
}

void fragment() {
	vec4 color = texture(main_texture, UV);
	ALBEDO = srgb_to_linear(color.rgb * xops_model_brightness);
	ALPHA = color.a;
}
