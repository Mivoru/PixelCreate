// A flat colour that fades by dropping pixels in a Bayer pattern.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform vec3 color : source_color = vec3(1.0);
uniform float strength = 1.0;

const float BAYER[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0,
                                  3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

void fragment() {
	ivec2 cell = ivec2(FRAGCOORD.xy) % 4;
	if (strength <= (BAYER[cell.y * 4 + cell.x] + 0.5) / 16.0) {
		discard;
	}
	// Matching the ground normal keeps the outline pass from framing the effect.
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = color;
}
