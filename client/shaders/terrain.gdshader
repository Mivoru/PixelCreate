// Ground: grass with dry patches, paths and rock. Vertex colour: red = path, green = rock.
shader_type spatial;

uniform sampler2D grass_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dry_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dirt_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;

varying vec3 world_position;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 uv = world_position.xz / 7.0;
	float large = texture(dry_tex, world_position.xz / 96.0).g;
	float grain = texture(grass_tex, world_position.xz / 11.0).g;
	vec3 color = texture(grass_tex, uv).rgb;
	if (large + grain * 0.35 > 0.86) {
		color = texture(dry_tex, uv).rgb;
	}
	// Noise on the threshold gives ragged edges instead of straight blends.
	if (COLOR.g + (grain - 0.5) * 0.5 > 0.5) {
		// Steep ground takes its texture from the side, so it does not smear into streaks.
		// Two rotated scales hide the repeat of the texture across a whole mountain side.
		vec2 side = vec2(world_position.x + world_position.z, world_position.y * 1.4) / 9.0;
		vec2 wide = vec2(world_position.x * 0.7 - world_position.z * 0.7, world_position.y) / 31.0;
		color = mix(texture(rock_tex, side + large * 0.6).rgb, texture(rock_tex, wide).rgb, 0.55) * vec3(1.06, 1.02, 0.96);
		// Bands of harder and softer rock, and snow on the high ground.
		color *= 0.9 + 0.14 * sin(world_position.y * 0.8 + large * 7.0);
		if (world_position.y + grain * 9.0 + large * 8.0 > 42.0) {
			color = vec3(0.9, 0.94, 0.98);
		}
	}
	if (COLOR.r + (grain - 0.5) * 0.45 > 0.5) {
		color = texture(dirt_tex, uv).rgb;
	}
	ALBEDO = color;
	ROUGHNESS = 1.0;
	SPECULAR = 0.1;
}
