// Grass blades in a MultiMesh. The tip bends with the wind; the root stays on the ground.
shader_type spatial;
render_mode cull_disabled;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float wave = sin(TIME * 2.1 + world.x * 0.5 + world.z * 0.4) + sin(TIME * 3.7 + world.z * 0.9) * 0.4;
	VERTEX.x += wave * VERTEX.y * 0.28;
	VERTEX.z += wave * VERTEX.y * 0.14;
}

void fragment() {
	// Light the blades like the ground, so they blend in and draw no outlines.
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 1.0;
	SPECULAR = 0.0;
}
