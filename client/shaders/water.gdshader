// Moving water: deep teal in the middle, the sky mirrored at a low angle, thin streaks of current,
// sun glints and pale foam along the banks. V runs across the stream, U along it. For a waterfall
// the flow is faster and whiter.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float flow = 0.25;
uniform float foam = 0.0;

varying vec3 world_position;

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

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float move = TIME * flow;
	float a = noise(vec2(UV.x * 5.0 - move * 9.0, UV.y * 16.0));
	float b = noise(vec2(UV.x * 11.0 - move * 15.0 + 4.0, UV.y * 34.0 + 2.0));
	float c = noise(vec2(UV.x * 1.3 - move * 3.0, UV.y * 4.0 + 7.0));
	float ripple = a * 0.45 + b * 0.3 + c * 0.25;
	float edge = abs(UV.y - 0.5) * 2.0;

	vec3 deep = vec3(0.03, 0.2, 0.3);
	vec3 shallow = vec3(0.1, 0.42, 0.46);
	vec3 color = mix(deep, shallow, clamp(edge * 0.8 + (ripple - 0.5) * 0.7, 0.0, 1.0));

	// The sky shows on the water, more and more towards the horizon.
	vec3 eye = normalize((INV_VIEW_MATRIX * vec4(VERTEX, 0.0)).xyz);
	float grazing = pow(1.0 - abs(eye.y), 3.0);
	color = mix(color, vec3(0.62, 0.82, 0.95), grazing * 0.45);

	// Thin bright lines of current, and glints that come and go.
	color = mix(color, vec3(0.55, 0.82, 0.86), smoothstep(0.66, 0.69, ripple) * 0.65);
	float glint = noise(world_position.xz * 3.1 + vec2(TIME * 0.6, -TIME * 0.4));
	color = mix(color, vec3(1.0, 0.99, 0.92), smoothstep(0.82, 0.86, glint * (0.6 + ripple * 0.8)));

	color = mix(color, vec3(0.88, 0.95, 0.96), smoothstep(0.88, 1.0, edge + (ripple - 0.5) * 0.25) * 0.8);
	color = mix(color, vec3(0.92, 0.97, 1.0), foam * smoothstep(0.32, 0.62, ripple));
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = color;
}
