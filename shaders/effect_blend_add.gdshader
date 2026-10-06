// 이펙트 빌보드용 가산 블렌딩 셰이더. effect_blend 와 같은 계산에 섞는 방식만 다르다.
// Godot 의 blend_mix / blend_add 는 컴파일 타임 설정이라 유니폼으로 바꿀 수 없어 셰이더를 따로 둔다.
// 원본 XOPS 에는 가산 블렌딩이 없다. 발광하는 이펙트(불꽃, 섬광)를 모더가 만들 수 있게 더한 것이다.
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, fog_disabled;

// source_color 를 붙이지 않는다 — sRGB 값을 그대로 받아 alpha 쪽 셰이더와 같은 공간에서 계산하기 위해서다.
uniform sampler2D main_texture : hint_default_white, filter_linear_mipmap, repeat_disable;
// 이펙트 하나하나의 투명도. 같은 머티리얼을 공유하면서 노드마다 다른 값을 준다.
instance uniform float effect_alpha = 1.0;
// 발광 세기 (EffectEmitter.brightness). 가산 블렌딩에서만 쓴다.
instance uniform float effect_bright = 1.0;

global uniform vec4 xops_fog_color;
global uniform vec2 xops_fog_range;

vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

void fragment() {
	vec4 col = texture(main_texture, UV);

	// 가산 블렌딩에서는 안개 색을 섞으면 오히려 밝아지므로, 멀어질수록 더하는 양 자체를 줄여 안개에 묻히게 한다.
	float depth = -VERTEX.z;
	float fog = clamp((depth - xops_fog_range.x) / max(xops_fog_range.y - xops_fog_range.x, 0.0001), 0.0, 1.0) * xops_fog_color.a;
	col.rgb *= 1.0 - fog;

	ALBEDO = srgb_to_linear(clamp(col.rgb * clamp(effect_bright, 0.0, 1.0), 0.0, 1.0));
	ALPHA = col.a * clamp(effect_alpha, 0.0, 1.0);
}
