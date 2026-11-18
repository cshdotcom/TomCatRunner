using UnityEngine;

namespace TomCatRunner.Gameplay.World
{
    /// <summary>
    /// 猫建模器 - 用 Unity 图元拼装一只橘白小猫
    /// 身体、头、耳、鼻、眼、条纹、四腿、三节尾巴、红项圈金铃铛
    /// </summary>
    public static class CatBuilder
    {
        // 橘猫配色
        private static readonly Color FurMain = new Color(0.93f, 0.55f, 0.22f);   // 橘
        private static readonly Color FurDark = new Color(0.72f, 0.38f, 0.13f);    // 深橘条纹
        private static readonly Color FurWhite = new Color(0.98f, 0.96f, 0.92f);   // 白肚白爪
        private static readonly Color InnerEar = new Color(1f, 0.72f, 0.72f);      // 粉耳内
        private static readonly Color NosePink = new Color(0.95f, 0.55f, 0.6f);
        private static readonly Color EyeBlack = new Color(0.1f, 0.08f, 0.08f);
        private static readonly Color EyeGlint = new Color(1f, 1f, 0.95f, 1f);
        private static readonly Color CollarRed = new Color(0.85f, 0.18f, 0.2f);
        private static readonly Color BellGold = new Color(1f, 0.8f, 0.25f);

        /// <summary>构建完整猫（返回已挂 CatController 的根物体）</summary>
        public static CatController Build(Transform parent, string name = "TomCat")
        {
            var rootGo = new GameObject(name);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = Vector3.zero;

            // 碰撞体（触发器）+ 运动学刚体
            var rb = rootGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            var col = rootGo.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.36f;
            col.height = 1.4f;
            col.center = new Vector3(0f, 0.7f, 0f);
            col.direction = 1; // Y 轴

            var controller = rootGo.AddComponent<CatController>();
            var visuals = new CatVisuals();
            BuildModel(rootGo.transform, visuals);
            controller.Visuals = visuals;
            return controller;
        }

        private static void BuildModel(Transform root, CatVisuals v)
        {
            var model = WorldPrimitives.Node(root, "Model", new Vector3(0f, 0.62f, 0f));

            var renderers = new System.Collections.Generic.List<Renderer>();

            // ---- 身体（胶囊水平放）----
            var body = WorldPrimitives.Node(model, "Body", new Vector3(0f, 0.1f, 0f));
            var bodyMesh = WorldPrimitives.Part(body.transform, PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.62f, 0.5f, 0.78f), FurMain, name: "BodyMesh");
            bodyMesh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 胶囊躺倒指向 Z
            renderers.Add(bodyMesh.GetComponent<Renderer>());
            v.Body = body.transform;

            // 白肚皮
            var belly = WorldPrimitives.Part(model, PrimitiveType.Sphere,
                new Vector3(0f, -0.08f, 0.12f), new Vector3(0.5f, 0.42f, 0.55f), FurWhite, castShadow: false, name: "Belly");
            renderers.Add(belly.GetComponent<Renderer>());

            // 背部条纹（三个薄片）
            for (int i = 0; i < 3; i++)
            {
                var stripe = WorldPrimitives.Part(model, PrimitiveType.Cube,
                    new Vector3(0f, 0.3f, -0.18f + i * 0.22f), new Vector3(0.3f - i * 0.04f, 0.1f, 0.12f),
                    FurDark, castShadow: false, name: $"Stripe{i}");
                renderers.Add(stripe.GetComponent<Renderer>());
            }

            // ---- 头 ----
            var head = WorldPrimitives.Node(model, "Head", new Vector3(0f, 0.42f, 0.34f));
            var headMesh = WorldPrimitives.Part(head, PrimitiveType.Sphere,
                Vector3.zero, new Vector3(0.5f, 0.46f, 0.46f), FurMain, name: "HeadMesh");
            renderers.Add(headMesh.GetComponent<Renderer>());
            v.Head = head.transform;

            // 口鼻（白）
            WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(0f, -0.08f, 0.19f), new Vector3(0.26f, 0.2f, 0.2f), FurWhite, castShadow: false, name: "Muzzle");
            // 鼻子（粉）
            WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(0f, -0.02f, 0.3f), new Vector3(0.07f, 0.06f, 0.05f), NosePink, castShadow: false, name: "Nose");

            // 眼睛（黑 + 高光点）
            var eyeL = WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(-0.13f, 0.08f, 0.2f), new Vector3(0.1f, 0.12f, 0.08f), EyeBlack, castShadow: false, emission: 0.35f, name: "EyeL");
            var eyeR = WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(0.13f, 0.08f, 0.2f), new Vector3(0.1f, 0.12f, 0.08f), EyeBlack, castShadow: false, emission: 0.35f, name: "EyeR");
            WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(-0.115f, 0.115f, 0.24f), new Vector3(0.035f, 0.04f, 0.02f), EyeGlint, castShadow: false, name: "GlintL");
            WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(0.145f, 0.115f, 0.24f), new Vector3(0.035f, 0.04f, 0.02f), EyeGlint, castShadow: false, name: "GlintR");

            // 耳朵（外橘内粉）
            var earL = WorldPrimitives.Node(head, "EarL", new Vector3(-0.16f, 0.24f, 0.02f));
            WorldPrimitives.Part(earL, PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.16f, 0.22f, 0.05f), FurMain, name: "EarLMesh");
            WorldPrimitives.Part(earL, PrimitiveType.Cube,
                new Vector3(0f, 0.01f, 0.02f), new Vector3(0.1f, 0.15f, 0.02f), InnerEar, castShadow: false, name: "EarLInner");
            earL.localRotation = Quaternion.Euler(-8f, 0f, 16f);
            v.EarLeft = earL;

            var earR = WorldPrimitives.Node(head, "EarR", new Vector3(0.16f, 0.24f, 0.02f));
            WorldPrimitives.Part(earR, PrimitiveType.Cube,
                Vector3.zero, new Vector3(0.16f, 0.22f, 0.05f), FurMain, name: "EarRMesh");
            WorldPrimitives.Part(earR, PrimitiveType.Cube,
                new Vector3(0f, 0.01f, 0.02f), new Vector3(0.1f, 0.15f, 0.02f), InnerEar, castShadow: false, name: "EarRInner");
            earR.localRotation = Quaternion.Euler(-8f, 0f, -16f);
            v.EarRight = earR;

            // ---- 项圈 + 铃铛 ----
            var collar = WorldPrimitives.Part(head, PrimitiveType.Cylinder,
                new Vector3(0f, -0.2f, -0.06f), new Vector3(0.27f, 0.04f, 0.27f), CollarRed, name: "Collar");
            collar.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var bell = WorldPrimitives.Part(head, PrimitiveType.Sphere,
                new Vector3(0f, -0.28f, 0.02f), new Vector3(0.09f, 0.09f, 0.09f), BellGold, emission: 0.25f, name: "Bell");

            // ---- 四条腿（白爪尖）----
            v.LegFL = BuildLeg(model, "LegFL", new Vector3(-0.2f, -0.22f, 0.22f));
            v.LegFR = BuildLeg(model, "LegFR", new Vector3(0.2f, -0.22f, 0.22f));
            v.LegBL = BuildLeg(model, "LegBL", new Vector3(-0.22f, -0.24f, -0.2f));
            v.LegBR = BuildLeg(model, "LegBR", new Vector3(0.22f, -0.24f, -0.2f));

            // ---- 尾巴（三节）----
            var tailRoot = WorldPrimitives.Node(model, "TailRoot", new Vector3(0f, 0.16f, -0.38f));
            WorldPrimitives.Part(tailRoot, PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.12f, 0.2f, 0.12f), FurMain, name: "TailSeg0");
            tailRoot.localRotation = Quaternion.Euler(-8f, 0f, -14f);
            v.TailRoot = tailRoot;

            var tailMid = WorldPrimitives.Node(tailRoot, "TailMid", new Vector3(0.02f, 0.16f, -0.06f));
            WorldPrimitives.Part(tailMid, PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.1f, 0.18f, 0.1f), FurMain, name: "TailSeg1");
            tailMid.localRotation = Quaternion.Euler(-4f, 0f, 30f);
            v.TailMid = tailMid;

            var tailTip = WorldPrimitives.Node(tailMid, "TailTip", new Vector3(0.06f, 0.14f, -0.04f));
            WorldPrimitives.Part(tailTip, PrimitiveType.Capsule,
                Vector3.zero, new Vector3(0.08f, 0.14f, 0.08f), FurWhite, name: "TailSeg2");
            tailTip.localRotation = Quaternion.Euler(4f, 0f, 28f);
            v.TailTip = tailTip;

            // 阴影代理（软圆形贴地 - 增强落地感）
            var shadow = WorldPrimitives.Part(root, PrimitiveType.Cylinder,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.9f, 0.005f, 0.9f),
                new Color(0f, 0f, 0f, 0.28f), castShadow: false, name: "BlobShadow");

            v.Model = model;
            v.Renderers = renderers.ToArray();
        }

        private static Transform BuildLeg(Transform parent, string name, Vector3 pos)
        {
            var leg = WorldPrimitives.Node(parent, name, pos);
            // 上半橘色
            WorldPrimitives.Part(leg, PrimitiveType.Capsule,
                new Vector3(0f, -0.08f, 0f), new Vector3(0.13f, 0.16f, 0.13f), FurMain, name: name + "_Upper");
            // 下半白爪
            WorldPrimitives.Part(leg, PrimitiveType.Capsule,
                new Vector3(0f, -0.22f, 0.02f), new Vector3(0.115f, 0.1f, 0.13f), FurWhite, name: name + "_Paw");
            return leg;
        }
    }
}
