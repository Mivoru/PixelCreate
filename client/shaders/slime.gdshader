// Jelly body: the surface wobbles, the rim is lighter and a wet highlight sits on top.
shader_type spatial;

uniform vec3 body : source_color = vec3(0.3, 0.8, 0.4);
uniform vec3 rim : source_color = vec3(0.7, 1.0, 0.7);
uniform vec3 core : source_color = vec3(0.1, 0.4, 0.3);
uniform float wobble = 0.05;
uniform float flash = 0.0;
uniform float glow = 0.0;

void vertex() {
	float wave = sin(TIME * 5.0 + VERTEX.y * 7.0) + sin(TIME * 3.1 + VERTEX.x * 6.0 + VERTEX.z * 5.0);
	VERTEX += NORMAL * wave * wobble;
}

void fragment() {
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 color = mix(rim, body, smoothstep(0.1, 0.55, facing));
	color = mix(color, core, smoothstep(0.75, 1.0, facing) * 0.6);
	ALBEDO = mix(color, vec3(1.0), flash);
	EMISSION = color * glow;
	ROUGHNESS = 0.25;
	SPECULAR = 0.7;
}
