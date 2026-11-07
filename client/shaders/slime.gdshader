// Jelly body: a gumdrop with a soft tip, a wobbling surface, a lighter rim, a darker heart deep
// inside, slow bubbles drifting up and a wet highlight on top.
shader_type spatial;

uniform vec3 body : source_color = vec3(0.3, 0.8, 0.4);
uniform vec3 rim : source_color = vec3(0.7, 1.0, 0.7);
uniform vec3 core : source_color = vec3(0.1, 0.4, 0.3);
uniform float wobble = 0.05;
uniform float flash = 0.0;
uniform float glow = 0.0;

varying vec3 local;

void vertex() {
	// Pinch the top into a soft tip and flatten the bottom where it sits on the ground.
	float up = clamp(VERTEX.y / 0.6, -1.0, 1.0);
	VERTEX.xz *= 1.0 - max(up, 0.0) * max(up, 0.0) * 0.35;
	VERTEX.y += pow(max(up, 0.0), 4.0) * 0.16;
	VERTEX.y = max(VERTEX.y, -0.46);
	float wave = sin(TIME * 5.0 + VERTEX.y * 7.0) + sin(TIME * 3.1 + VERTEX.x * 6.0 + VERTEX.z * 5.0);
	VERTEX += NORMAL * wave * wobble;
	local = VERTEX;
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 color = mix(rim, body, smoothstep(0.1, 0.55, facing));
	color = mix(color, core, smoothstep(0.7, 1.0, facing) * 0.55);

	// Bubbles: small round spots that rise through the jelly and fade near the edge.
	vec3 p = local * vec3(9.0, 7.0, 9.0) + vec3(0.0, -TIME * 0.6, 0.0);
	vec3 cell = fract(p) - 0.5;
	float seed = fract(sin(dot(floor(p), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
	float bubble = step(0.82, seed) * (1.0 - smoothstep(0.12, 0.2, length(cell.xy)));
	color = mix(color, rim + 0.15, bubble * facing * 0.6);

	ALBEDO = mix(color, vec3(1.0), flash);
	EMISSION = color * glow;
	ROUGHNESS = 0.2;
	SPECULAR = 0.8;
	RIM = 0.4;
	RIM_TINT = 0.6;
}
