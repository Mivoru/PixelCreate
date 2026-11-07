// Cel shading: exactly two tones with a crisp edge and a thin bright rim, as in anime. The colours
// come from the material alone, so the sky cannot tint the character. Hair and cloth bend in the
// vertex stage: the further a vertex is below sway_top, the more it follows sway and flutters.
shader_type spatial;
render_mode ambient_light_disabled, cull_disabled;

uniform vec3 base : source_color = vec3(1.0);
uniform vec3 shade : source_color = vec3(0.6);
uniform float rim_strength = 0.3;
uniform vec3 sway = vec3(0.0);
uniform float sway_top = 0.0;
uniform float sway_length = 1.0;
uniform float flutter = 0.0;

void vertex() {
	float t = clamp((sway_top - VERTEX.y) / sway_length, 0.0, 1.0);
	float wave = sin(TIME * 8.0 + VERTEX.x * 11.0 + t * 7.0) + sin(TIME * 5.0 - VERTEX.x * 6.0 + t * 3.0) * 0.6;
	VERTEX += sway * t * t + vec3(0.0, 0.0, wave * flutter * t);
}

void fragment() {
	EMISSION = shade;
	ALBEDO = max(base - shade, vec3(0.0));
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float lit = smoothstep(0.1, 0.16, dot(NORMAL, LIGHT) * ATTENUATION);
	DIFFUSE_LIGHT += vec3(lit);
	SPECULAR_LIGHT += vec3(pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0) * lit * rim_strength);
}
