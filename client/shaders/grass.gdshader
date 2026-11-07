// Grass blades in a MultiMesh. A slow gust rolls across the meadow, bending the blades and lighting
// their tips, so the wind can be seen moving through the grass. Roots take the colour of the ground.
shader_type spatial;
render_mode cull_disabled;

varying float tip;
varying float gust;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	tip = clamp(VERTEX.y / 0.46, 0.0, 1.0);
	float roll = sin(world.x * 0.11 + world.z * 0.07 - TIME * 1.1) * 0.5 + 0.5;
	gust = roll * roll;
	float ripple = sin(TIME * 2.6 + world.x * 0.8 + world.z * 0.6);
	float bend = tip * tip * (0.1 + gust * 0.34) + ripple * tip * 0.05;
	VERTEX.x += bend;
	VERTEX.z += bend * 0.5;
}

void fragment() {
	// Light the blades like the ground, so they blend in and draw no outlines.
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	vec3 root = COLOR.rgb * vec3(0.5, 0.62, 0.55);
	vec3 top = COLOR.rgb * vec3(1.12, 1.14, 0.92) + vec3(0.1, 0.1, 0.02) * gust;
	ALBEDO = mix(root, top, tip);
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
