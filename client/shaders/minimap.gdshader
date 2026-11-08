// The minimap: a window onto the top-down picture of the zone, cut to a circle with a soft rim.
// Centre is the player's place on the picture (0 to 1), span how much of the picture the circle shows.
shader_type canvas_item;

uniform vec2 centre = vec2(0.5);
uniform float span = 0.2;
uniform vec4 rim : source_color = vec4(0.95, 0.82, 0.5, 1.0);

void fragment() {
	vec2 offset = UV - vec2(0.5);
	float distance = length(offset) * 2.0;
	if (distance > 1.0) {
		discard;
	}
	vec2 at = centre + offset * span;
	vec4 picture = texture(TEXTURE, at);
	// Beyond the picture's edge lies nothing mapped: a dark ground.
	if (at.x < 0.0 || at.y < 0.0 || at.x > 1.0 || at.y > 1.0) {
		picture = vec4(0.09, 0.1, 0.13, 1.0);
	}
	COLOR = mix(picture, rim, smoothstep(0.93, 0.97, distance));
	COLOR.a = 1.0;
}
