Shader "Custom/ToonShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _RampThreshold ("Ramp Threshold", Range(0,1)) = 0.5
        _RampSmoothness ("Ramp Smoothness", Range(0,1)) = 0.1
        
        [Header(Rim Lighting)]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        
        [Header(Specular)]
        _SpecularColor ("Specular Color", Color) = (0.9,0.9,0.9,1)
        _Glossiness ("Glossiness", Range(0.1, 500)) = 10
        
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Toon fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        fixed4 _Color;
        float _RampThreshold;
        float _RampSmoothness;
        
        fixed4 _RimColor;
        float _RimPower;
        
        fixed4 _SpecularColor;
        float _Glossiness;

        // Custom Toon Lighting Model
        half4 LightingToon (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            // Diffuse term
            half NdotL = dot (s.Normal, lightDir);
            // Remap NdotL to stay within [0, 1] mostly or handle negatives
            // But for simple toon, we just want to know how much light we get.
            
            // Toon Ramp
            // Usage of smoothstep gives us a configurable "soft" edge for the banding
            half diff = smoothstep(_RampThreshold - _RampSmoothness, _RampThreshold + _RampSmoothness, NdotL);
            
            // Specular term
            half3 h = normalize (lightDir + viewDir);
            float NdotH = dot (s.Normal, h);
            float specularIntensity = pow (max (0, NdotH), _Glossiness * _Glossiness);
            // Sharpen the specular highlight for a cartoon feel
            float specular = smoothstep(0.005, 0.01, specularIntensity);
            
            half4 c;
            c.rgb = (s.Albedo * _LightColor0.rgb * diff + _LightColor0.rgb * _SpecularColor.rgb * specular) * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            
            // Rim Lighting
            // Calculate how much the surface normal points away from the camera
            half rim = 1.0 - saturate(dot (normalize(IN.viewDir), o.Normal));
            // Power it to control width
            rim = pow (rim, _RimPower);
            
            // Add rim to emission so it glows
            o.Emission = _RimColor.rgb * rim;
            
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
