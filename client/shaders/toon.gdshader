// Cel shading: exactly two tones with a crisp edge and a thin bright rim, as in anime. The colours
// come from the material alone, so the sky cannot tint the character. Hair and cloth bend in the
// vertex stage along a small grid of offsets that ClothChain simulates: 3 columns of 6 rows.
shader_type spatial;
render_mode ambient_light_disabled, cull_disabled;

uniform vec3 base : source_color = vec3(1.0);
uniform vec3 shade : source_color = vec3(0.6);
uniform float rim_strength = 0.3;
uniform float threshold = 0.1;
uniform vec3 chain[18];
uniform float chain_on = 0.0;
uniform float chain_width = 0.2;
uniform float back_bias = 0.0;
uniform float shine = 0.0;
uniform float sway_top = 0.0;
uniform float sway_length = 1.0;
// Takes the colour from the vertices instead (sRGB), shaded towards the same violet as Toon() in code.
uniform float vertex_tint = 0.0;

vec3 along(int column, float t) {
	int row = min(int(t), 4);
	return mix(chain[column * 6 + row], chain[column * 6 + row + 1], t - float(row));
}

void vertex() {
	if (chain_on > 0.5) {
		float t = clamp((sway_top - VERTEX.y) / sway_length, 0.0, 1.0) * 5.0;
		float s = clamp(VERTEX.x / chain_width, -1.0, 1.0);
		vec3 bend = s < 0.0 ? mix(along(1, t), along(0, t), -s) : mix(along(1, t), along(2, t), s);
		// Hair in front of the face stays calmer than hair at the back.
		VERTEX += bend * mix(1.0, smoothstep(-0.13, 0.05, VERTEX.z) * 0.8 + 0.2, back_bias);
	}
}

void fragment() {
	vec3 b = base;
	vec3 s = shade;
	if (vertex_tint > 0.5) {
		b = pow(COLOR.rgb, vec3(2.2));
		s = mix(b, pow(vec3(0.29, 0.243, 0.47), vec3(2.2)), 0.3);
	}
	EMISSION = s;
	ALBEDO = max(b - s, vec3(0.0));
	// Anime hair carries a ring of light across the crown, broken into points along the strands.
	if (shine > 0.5) {
		vec3 up = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
		float lift = dot(NORMAL, up);
		vec3 world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
		float teeth = sin(world.x * 140.0 + world.z * 90.0) * 0.05;
		float ring = smoothstep(0.34, 0.4, lift + teeth) * (1.0 - smoothstep(0.62, 0.68, lift - teeth)) * step(0.15, NORMAL.z);
		EMISSION = mix(EMISSION, mix(b, vec3(1.0), 0.45), ring * 0.85);
	}
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}

void light() {
	float lit = smoothstep(threshold, threshold + 0.06, dot(NORMAL, LIGHT) * ATTENUATION);
	DIFFUSE_LIGHT += vec3(lit);
	SPECULAR_LIGHT += vec3(pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 4.0) * lit * rim_strength);
}
