// 스카이 메시 셰이더. 메시를 항상 카메라 위치에 둔 것처럼 그려 배경으로 쓴다.
// 깊이를 읽지도 쓰지도 않고 불투명 패스에서 먼저 그려지므로, 뒤에 그려지는 맵이 항상 위에 덮인다. 안개는 받지 않는다.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, depth_test_disabled, fog_disabled, skip_vertex_transform;

uniform sampler2D main_texture : source_color, hint_default_black, filter_linear, repeat_enable;

void vertex() {
	// 원본 XOPS 기준으로 Y축 180도 회전 보정 (X, Z 반전).
	vec3 direction = vec3(-VERTEX.x, VERTEX.y, -VERTEX.z);

	// w = 0 으로 곱해 카메라 회전만 적용한다(이동 무시) → 메시가 카메라를 따라다닌다.
	VERTEX = (VIEW_MATRIX * vec4(direction, 0.0)).xyz;
	POSITION = PROJECTION_MATRIX * vec4(VERTEX, 1.0);
	// 먼 클리핑 면에 붙여 메시 크기와 무관하게 잘리지 않게 한다 (Godot 은 역방향 Z 라 먼 면이 0).
	POSITION.z = 0.0;
}

void fragment() {
	ALBEDO = texture(main_texture, UV).rgb;
}
