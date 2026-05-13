Shader "Custom/DepthTextureShader"
{
    Properties
    {
        _MainTex ("Depth Texture", 2D) = "white" {}
        _DepthMin ("Depth Min", Float) = 0.5
        _DepthMax ("Depth Max", Float) = 5.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            sampler2D _MainTex;
            float _DepthMin;
            float _DepthMax;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            float frag (v2f i) : SV_Target
            {
                // Sample depth from the explicit texture
                float depth01 = tex2D(_MainTex, i.uv).r;
                
                // Convert to linear depth in world units (meters)
                float linearDepth = LinearEyeDepth(depth01);
                
                // Clamp to valid range
                if (linearDepth < _DepthMin || linearDepth > _DepthMax)
                    return 0.0;
                    
                // Return actual distance in meters
                return linearDepth;
            }
            ENDCG
        }
    }
}