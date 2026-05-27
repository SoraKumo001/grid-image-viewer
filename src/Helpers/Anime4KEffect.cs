using quick_image_viewer.Common;
using SkiaSharp;
using System;

namespace quick_image_viewer.Helpers
{
    public static class Anime4KEffect
    {
        private static SKRuntimeEffect? _effect;
        private static readonly object _lock = new();

        // Anime4K: Edge Refinement (Thinning) shader written in SkiaSL (Skia Shading Language)
        private const string ShaderCode = @"
            uniform shader image;
            uniform vec2 inputSize;
            uniform float strength;

            // Compute luminance
            float get_luma(vec4 rgba) {
                return dot(rgba.rgb, vec3(0.299, 0.587, 0.114));
            }

            vec4 main(vec2 coords) {
                vec2 d = 1.0 / inputSize;
                
                // Sample 3x3 neighborhood luminance
                float m  = get_luma(image.eval(coords));
                float nw = get_luma(image.eval(coords + vec2(-d.x, -d.y)));
                float n  = get_luma(image.eval(coords + vec2(0.0,  -d.y)));
                float ne = get_luma(image.eval(coords + vec2(d.x,  -d.y)));
                float w  = get_luma(image.eval(coords + vec2(-d.x, 0.0)));
                float e  = get_luma(image.eval(coords + vec2(d.x,  0.0)));
                float sw = get_luma(image.eval(coords + vec2(-d.x, d.y)));
                float s  = get_luma(image.eval(coords + vec2(0.0,  d.y)));
                float se = get_luma(image.eval(coords + vec2(d.x,  d.y)));
                
                // Sobel operator
                float x = (ne + 2.0 * e + se) - (nw + 2.0 * w + sw);
                float y = (sw + 2.0 * s + se) - (nw + 2.0 * n + ne);
                
                vec2 grad = vec2(x, y);
                float len = length(grad);
                
                if (len > 0.0001) {
                    grad = grad / len;
                    // Shift coordinates along the gradient direction to thin the edges
                    vec2 newCoords = coords - grad * d * strength;
                    return image.eval(newCoords);
                }
                return image.eval(coords);
            }
        ";

        public static SKRuntimeEffect GetEffect()
        {
            if (_effect == null)
            {
                lock (_lock)
                {
                    if (_effect == null)
                    {
                        // In SkiaSharp 2.88.x, use CreateShader instead of Create
                        _effect = SKRuntimeEffect.CreateShader(ShaderCode, out string errors);
                        if (_effect == null)
                        {
                            AppLog.Error("Anime4KEffect", $"Failed to compile Anime4K shader: {errors}");
                            throw new Exception($"Failed to compile Anime4K shader: {errors}");
                        }
                    }
                }
            }
            return _effect;
        }

        public static SKShader? CreateShader(SKImage image, float strength = 1.0f)
        {
            try
            {
                var effect = GetEffect();
                var uniforms = new SKRuntimeEffectUniforms(effect)
                {
                    { "inputSize", new float[] { image.Width, image.Height } },
                    { "strength", strength }
                };

                var imageShader = image.ToShader();
                var children = new SKRuntimeEffectChildren(effect)
                {
                    { "image", imageShader }
                };

                return effect.ToShader(uniforms, children);
            }
            catch (Exception ex)
            {
                AppLog.Error("Anime4KEffect", "CreateShader (SKImage) failed", ex);
                return null;
            }
        }

        public static SKShader? CreateShader(SKBitmap bitmap, float strength = 1.0f)
        {
            try
            {
                var effect = GetEffect();
                var uniforms = new SKRuntimeEffectUniforms(effect)
                {
                    { "inputSize", new float[] { bitmap.Width, bitmap.Height } },
                    { "strength", strength }
                };

                var imageShader = SKShader.CreateBitmap(bitmap);
                var children = new SKRuntimeEffectChildren(effect)
                {
                    { "image", imageShader }
                };

                return effect.ToShader(uniforms, children);
            }
            catch (Exception ex)
            {
                AppLog.Error("Anime4KEffect", "CreateShader (SKBitmap) failed", ex);
                return null;
            }
        }
    }
}
