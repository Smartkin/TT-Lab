// Add here any uniforms you would like to be accessible from any shader type

uniform float Time;
uniform vec2 Resolution;
uniform vec3 EyePosition;
uniform vec3 EyeDirection;
// The chunk's fog (GameFog): 0 none, 1 the PS2's colour filter, 2 the Xbox's fog stage
uniform int FogMode;
// The skydome, which the game draws around the camera
uniform bool FollowsCamera;
uniform float Fov; // In radians
uniform float Aspect;