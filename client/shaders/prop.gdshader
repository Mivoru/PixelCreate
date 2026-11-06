// Props: a flat colour with a world-aligned pixel detail texture, and optional sway for leaves.
shader_type spatial;

uniform vec3 tint : source_color = vec3(1.0);
uniform sampler2D detail : source_color, filter_nearest, repeat_enable;
uniform float detail_scale = 0.5;
uniform float sway = 0.0;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float wave = sin(TIME * 1.7 + world.x * 0.35 + world.z * 0.27);
	float flutter = sin(TIME * 4.3 + world.x * 1.3 + world.y * 1.9);
	VERTEX.x += (wave * 0.08 + flutter * 0.03) * sway;
	VERTEX.z += (wave * 0.05 - flutter * 0.03) * sway;
	world_position = world;
	world_normal = abs(normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz));
}

void fragment() {
	vec3 blend = world_normal / (world_normal.x + world_normal.y + world_normal.z);
	vec3 p = world_position * detail_scale;
	vec3 sample_color = texture(detail, p.zy).rgb * blend.x + texture(detail, p.xz).rgb * blend.y
		+ texture(detail, p.xy).rgb * blend.z;
	ALBEDO = tint * sample_color;
	ROUGHNESS = 1.0;
	SPECULAR = 0.1;
}
