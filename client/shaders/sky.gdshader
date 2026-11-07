// A bright anime sky: a soft gradient with hard-edged drifting clouds and a pale sun.
shader_type sky;

uniform vec3 top_color : source_color = vec3(0.24, 0.53, 0.86);
uniform vec3 horizon_color : source_color = vec3(0.78, 0.9, 0.96);
uniform vec3 cloud_color : source_color = vec3(1.0, 0.99, 0.96);
uniform vec3 cloud_shade : source_color = vec3(0.72, 0.8, 0.9);

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float clouds(vec2 p) {
	return noise(p) * 0.55 + noise(p * 2.1 + 3.0) * 0.3 + noise(p * 4.3 + 7.0) * 0.15;
}

void sky() {
	float up = clamp(EYEDIR.y, 0.0, 1.0);
	vec3 color = mix(horizon_color, top_color, pow(up, 0.55));
	if (EYEDIR.y > 0.01) {
		vec2 p = EYEDIR.xz / (EYEDIR.y + 0.18) * 1.6 + vec2(TIME * 0.006, TIME * 0.002);
		float body = clouds(p);
		float under = clouds(p + vec2(0.05, 0.09));
		float edge = smoothstep(0.03, 0.2, EYEDIR.y);
		color = mix(color, cloud_shade, step(0.56, body) * edge);
		color = mix(color, cloud_color, step(0.56, body) * step(under, body + 0.012) * edge);
	}
	if (EYEDIR.y < 0.0) {
		color = horizon_color;
	}
	COLOR = color;
}
