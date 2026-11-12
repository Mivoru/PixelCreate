// Ground: grass with dry patches, paths and rock. Vertex colour: red = path, green = rock,
// blue = forest floor, alpha = 1 - blight.
shader_type spatial;

uniform sampler2D grass_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dry_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D dirt_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D rock_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;

varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
	world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	vec2 uv = world_position.xz / 7.0;
	float large = texture(dry_tex, world_position.xz / 96.0).g;
	float grain = texture(grass_tex, world_position.xz / 11.0).g;
	vec3 color = texture(grass_tex, uv).rgb;
	if (large + grain * 0.35 > 0.86) {
		color = texture(dry_tex, uv).rgb;
	}
	// Wide washes of warmer and cooler green, as a painter would lay them down.
	float wash = texture(dry_tex, world_position.xz / 61.0 + 0.3).g;
	color *= mix(vec3(0.82, 0.96, 0.8), vec3(1.14, 1.08, 0.82), smoothstep(0.3, 0.75, wash));
	// Forest floor: dark moss and fallen needles, broken by patches of bare earth.
	float forest = COLOR.b;
	if (forest > 0.01) {
		vec3 needles = mix(vec3(0.17, 0.2, 0.12), vec3(0.29, 0.22, 0.15), smoothstep(0.35, 0.7, grain + large * 0.4));
		needles *= 0.85 + 0.3 * texture(dirt_tex, uv * 1.3).r;
		color = mix(color, needles, forest * 0.85);
	}
	// Blight drains the green out of the grass, leaving it grey and brown.
	float blight = 1.0 - COLOR.a;
	if (blight > 0.01) {
		float grey = dot(color, vec3(0.3, 0.55, 0.15));
		color = mix(color, vec3(grey * 0.95, grey * 0.88, grey * 0.72) * 0.8, blight * 0.8);
	}
	// Noise on the threshold gives ragged edges instead of straight blends.
	if (COLOR.g + (grain - 0.5) * 0.5 > 0.5) {
		// Steep ground takes its texture from the side, so it does not smear into streaks.
		// Two rotated scales hide the repeat of the texture across a whole mountain side.
		vec2 side = vec2(world_position.x + world_position.z, world_position.y * 1.4) / 9.0;
		vec2 wide = vec2(world_position.x * 0.7 - world_position.z * 0.7, world_position.y) / 31.0;
		color = mix(texture(rock_tex, side + large * 0.6).rgb, texture(rock_tex, wide).rgb, 0.55) * vec3(1.06, 1.02, 0.96);
		// Bands of harder and softer rock, warmer and cooler, so a cliff face shows its layers.
		float strata = sin(world_position.y * 0.8 + large * 7.0);
		color *= 0.88 + 0.16 * strata;
		color *= mix(vec3(0.94, 0.97, 1.04), vec3(1.07, 1.0, 0.9), smoothstep(-0.4, 0.6, sin(world_position.y * 0.23 + large * 3.0)));
		// Low banks cut by the streams are earth and pebbles, not mountain rock: brown, layered finely,
		// with moss where the slope eases and roots of grass hanging over the top edge.
		float low = 1.0 - smoothstep(3.0, 6.5, world_position.y);
		if (low > 0.01) {
			vec3 earth = texture(dirt_tex, side * 1.6).rgb * vec3(0.56, 0.46, 0.36);
			// Fine layers of darker and lighter earth, broken up so they do not run like ruled lines.
			float layer = sin(world_position.y * 4.2 + large * 11.0 + grain * 3.0);
			earth *= 0.78 + 0.3 * smoothstep(-0.6, 0.6, layer);
			// Stones bedded in the bank: lighter grey with a dark rim.
			float stone = texture(rock_tex, side * 2.3 + 0.4).g + grain * 0.3;
			earth = mix(earth, vec3(0.6, 0.6, 0.62), smoothstep(0.82, 0.86, stone));
			earth = mix(earth, earth * 0.55, smoothstep(0.76, 0.8, stone) - smoothstep(0.82, 0.86, stone));
			float moss = smoothstep(0.35, 0.65, world_normal.y + (grain - 0.5) * 0.5);
			earth = mix(earth, vec3(0.26, 0.38, 0.17), moss * 0.75);
			color = mix(color, earth, low * 0.85);
		}
	}
	// Snow settles on the ledges and only covers the tops: steep faces stay bare rock. High ledges
	// are flat enough to count as grass, so this runs on any ground.
	float high = world_position.y + grain * 7.0 + large * 6.0;
	float level = world_normal.y;
	if ((high > 46.0 && level > 0.8) || (high > 56.0 && level > 0.6) || high > 68.0) {
		color = vec3(0.9, 0.94, 0.98) * (0.93 + 0.07 * grain);
	}
	if (COLOR.r + (grain - 0.5) * 0.45 > 0.5) {
		color = texture(dirt_tex, uv).rgb;
	}
	ALBEDO = color;
	ROUGHNESS = 1.0;
	SPECULAR = 0.1;
}
