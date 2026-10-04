// 화면 전체의 밝기와 감마를 조정한다. 다른 모든 화면 요소 위에 놓인 전체 화면 사각형에 쓴다.
// 화면 텍스처는 sRGB 값이므로 선형으로 바꿔 계산한 뒤 되돌린다.
shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, filter_nearest;
// 밝기: 곱. 1.0 = 그대로.
uniform float brightness = 1.0;
// 감마: pow(c, gamma). 1.0 = 그대로, 클수록 어둡고 작을수록 밝다.
uniform float gamma = 1.0;

void fragment() {
	vec3 c = texture(screen_texture, SCREEN_UV).rgb;
	c = pow(c, vec3(2.2));
	c *= brightness;
	c = pow(max(c, vec3(0.0)), vec3(gamma));
	COLOR = vec4(pow(c, vec3(1.0 / 2.2)), 1.0);
}
