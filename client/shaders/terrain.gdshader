// Ground: grass with dry patches, paths and rock. Vertex colour: red = path, green = rock.
shader_type spatial;

uniform sampler2D grass_tex : source_color, filter_nearest, repeat_enable;
uniform sampler2D dry_tex : source_color, filter_nearest, repeat_enable;
uniform sampler2D dirt_tex : source_color, filter_nearest, repeat_enable;
uniform sampler2D rock_tex : source_color, filter_nearest, repeat_enable;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = world_position.xz / 6.0;
	float large = texture(dry_tex, world_position.xz / 96.0).g;
	float grain = texture(grass_tex, world_position.xz / 11.0).g;
	vec3 color = texture(grass_tex, uv).rgb;
	if (large + grain * 0.35 > 0.86) {
		color = texture(dry_tex, uv).rgb;
	}
	// Noise on the threshold gives ragged edges instead of straight blends.
	if (COLOR.g + (grain - 0.5) * 0.5 > 0.5) {
		color = texture(rock_tex, uv).rgb;
	}
	if (COLOR.r + (grain - 0.5) * 0.45 > 0.5) {
		color = texture(dirt_tex, uv).rgb;
	}
	ALBEDO = color;
	ROUGHNESS = 1.0;
	SPECULAR = 0.1;
}
