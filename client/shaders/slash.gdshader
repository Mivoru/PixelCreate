// The blade's path. U runs along the swing, V from the player out to the tip. It is brightest and
// most solid at the tip and the leading edge, and breaks up towards the player and the tail.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float head = 0.0;
uniform float tail_length = 0.75;
uniform float strength = 1.0;
uniform vec3 inner_color : source_color = vec3(1.0, 0.8, 0.4);
uniform vec3 tip_color : source_color = vec3(1.0);

const float BAYER[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0,
                                  3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);

void fragment() {
	float behind = head - UV.x;
	float along = behind < 0.0 ? 0.0 : 1.0 - clamp(behind / tail_length, 0.0, 1.0);
	float streaks = 0.75 + 0.25 * sin(UV.y * 34.0 + UV.x * 9.0);
	float alpha = pow(UV.y, 2.2) * along * streaks * strength;
	ivec2 cell = ivec2(FRAGCOORD.xy) % 4;
	if (alpha <= (BAYER[cell.y * 4 + cell.x] + 0.5) / 16.0) {
		discard;
	}
	NORMAL = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	ALBEDO = mix(inner_color, tip_color, smoothstep(0.55, 1.0, UV.y));
}
