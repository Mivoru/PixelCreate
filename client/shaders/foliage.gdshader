// Leaves on cut-out cards. The normals point away from the centre of each crown, so a tree shades
// like one soft mass rather than a pile of flat cards. Light is banded into painted steps, the
// underside stays deep but never black, and each tree takes a slightly different hue.
shader_type spatial;
render_mode cull_disabled;

uniform vec3 tint : source_color = vec3(0.4, 0.6, 0.3);
uniform vec3 deep : source_color = vec3(0.1, 0.22, 0.2);
uniform sampler2D leaves : source_color, filter_linear_mipmap, repeat_disable;
uniform float crown_low = 2.5;
uniform float crown_high = 7.0;
uniform float sway = 1.0;

varying float seed;
varying float height;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float scale = max(length(MODEL_MATRIX[1].xyz), 0.01);
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	seed = hash(origin.xz);
	height = clamp((VERTEX.y - crown_low) / (crown_high - crown_low), 0.0, 1.0);
	float gust = sin(TIME * 0.9 + world.x * 0.09 + world.z * 0.07) * 0.5 + 0.5;
	float wave = sin(TIME * 1.9 + world.x * 0.45 + world.z * 0.37 + seed * 6.0);
	float flutter = sin(TIME * 5.3 + world.x * 2.1 + world.y * 2.7);
	VERTEX.x += (wave * 0.09 * (0.4 + gust) + flutter * 0.025) * sway / scale;
	VERTEX.z += (wave * 0.06 * (0.4 + gust) - flutter * 0.025) * sway / scale;
}

void fragment() {
	vec4 sample_color = texture(leaves, UV);
	if (sample_color.a < 0.45) {
		discard;
	}
	// Leaves between the camera and the player dissolve, so a crown never blinds the view.
	// A clean cut, not a dither: at the pixel resolution a dither shows as a coarse checkerboard.
	if (-VERTEX.z < 2.2) {
		discard;
	}
	vec3 leaf = tint * (0.88 + seed * 0.24) * mix(vec3(1.0), vec3(1.08, 0.98, 0.8), seed * seed);
	vec3 color = mix(deep, leaf, 0.35 + height * 0.65) * (0.55 + sample_color.r * 0.75);
	ALBEDO = color;
	EMISSION = color * 0.28;
	// The post pass reads this low roughness as "leaves" and draws no outline inside a crown.
	ROUGHNESS = 0.5;
	SPECULAR = 0.0;
}

void light() {
	float facing = dot(NORMAL, LIGHT) * 0.5 + 0.5;
	float step_light = smoothstep(0.42, 0.5, facing) * 0.55 + smoothstep(0.66, 0.74, facing) * 0.45;
	DIFFUSE_LIGHT += LIGHT_COLOR * ATTENUATION * ALBEDO * step_light * 0.9 / 3.14159 * 3.14159;
}
