// A licking flame on a camera-facing quad. Noise climbs through a tapering shape, and heat picks
// the colour from deep red at the edge to pale yellow in the core.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float reach = 0.0;

varying float seed;
varying float camera_distance;

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

float fbm(vec2 p) {
	return noise(p) * 0.55 + noise(p * 2.1 + 3.7) * 0.3 + noise(p * 4.3 + 7.1) * 0.15;
}

void vertex() {
	seed = INSTANCE_CUSTOM.y;
	camera_distance = length(MODEL_MATRIX[3].xyz - CAMERA_POSITION_WORLD);

	// Turn around the vertical axis to face the camera, keeping the size of the instance.
	vec3 side = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
	vec3 depth = cross(side, vec3(0.0, 1.0, 0.0));
	float size = length(MODEL_MATRIX[1].xyz);

	// A flame rises from nothing as the front of the fire reaches it.
	float lit = clamp((reach - INSTANCE_CUSTOM.x) / 1.5, 0.0, 1.0);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(side * size, 0.0), vec4(0.0, size * lit, 0.0, 0.0),
		vec4(depth * size, 0.0), MODEL_MATRIX[3]);
}

void fragment() {
	float x = UV.x - 0.5;
	float up = 1.0 - UV.y;
	float time = TIME * 1.9 + seed * 17.0;
	float sway = (noise(vec2(seed * 9.0, up * 1.3 - time * 0.7)) - 0.5) * up * 0.5;
	float grain = fbm(vec2((x + sway) * 3.2 + seed * 5.0, up * 1.7 - time * 1.3));
	float body = (1.0 - pow(up, 1.2)) * 0.46 - abs(x + sway);
	float heat = body * 2.5 + (grain - 0.5) * 0.9 * (0.3 + up);
	if (heat < 0.02) {
		discard;
	}

	// Flames right in front of the camera thin out, so standing in the fire does not blind the player.
	ivec2 cell = ivec2(FRAGCOORD.xy) % 2;
	if (camera_distance < 5.0 && cell.x + cell.y * 2 < int((5.0 - camera_distance) * 1.6)) {
		discard;
	}

	vec3 color = mix(vec3(0.62, 0.07, 0.04), vec3(0.98, 0.33, 0.06), smoothstep(0.02, 0.3, heat));
	color = mix(color, vec3(1.0, 0.72, 0.18), smoothstep(0.26, 0.56, heat));
	color = mix(color, vec3(1.0, 0.95, 0.66), smoothstep(0.52, 0.86, heat));
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = color;
}
