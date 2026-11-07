// Moving water: layered ripples, darker in the middle, with pale foam along the banks.
// V runs across the stream, U along it. For a waterfall the flow is faster and whiter.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float flow = 0.25;
uniform float foam = 0.0;

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

void fragment() {
	float move = TIME * flow;
	float a = noise(vec2(UV.x * 5.0 - move * 9.0, UV.y * 16.0));
	float b = noise(vec2(UV.x * 11.0 - move * 15.0 + 4.0, UV.y * 34.0 + 2.0));
	float c = noise(vec2(UV.x * 1.3 - move * 3.0, UV.y * 4.0 + 7.0));
	float ripple = a * 0.45 + b * 0.3 + c * 0.25;
	float edge = abs(UV.y - 0.5) * 2.0;
	vec3 color = mix(vec3(0.05, 0.27, 0.42), vec3(0.14, 0.5, 0.62), ripple);
	color = mix(color, vec3(0.62, 0.86, 0.9), smoothstep(0.64, 0.68, ripple) * 0.75);
	color = mix(color, vec3(0.9, 0.96, 0.97), smoothstep(0.82, 1.0, edge + (ripple - 0.5) * 0.3) * 0.8);
	color = mix(color, vec3(0.92, 0.97, 1.0), foam * smoothstep(0.32, 0.62, ripple));
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = color;
}
