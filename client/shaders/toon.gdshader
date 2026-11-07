// Cel shading: exactly two tones with a crisp edge and a thin bright rim, as in anime. The colours
// come from the material alone, so the sky cannot tint the character.
shader_type spatial;
render_mode ambient_light_disabled;

uniform vec3 base : source_color = vec3(1.0);
uniform vec3 shade : source_color = vec3(0.6);
uniform float rim_strength = 0.3;

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
