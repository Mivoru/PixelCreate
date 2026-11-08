// A see-through spirit: a cold body colour that brightens to a glowing rim at the edges, with
// pixels dropped in a Bayer pattern so it reads as half there without any blending.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled;

uniform vec3 body : source_color = vec3(0.35, 0.45, 0.7);
uniform vec3 rim : source_color = vec3(0.75, 0.9, 1.0);
uniform float strength = 0.7;

const float BAYER[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0,
                                  3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

void fragment() {
	float edge = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.6);
	ivec2 cell = ivec2(FRAGCOORD.xy) % 4;
	// The rim stays solid, so the outline of the wolf holds while its middle thins out.
	if (min(1.0, strength + edge * 0.5) <= (BAYER[cell.y * 4 + cell.x] + 0.5) / 16.0) {
		discard;
	}
	ALBEDO = mix(body, rim, step(0.7, edge));
}
