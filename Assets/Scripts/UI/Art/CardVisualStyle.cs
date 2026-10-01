// 职责：ArtTest和正式Choice共用的可编辑卡面参数；不包含业务数据。
// 依赖：UnityEngine；维护Dada，交接docs/handoffs/Dada.handoff；规范AGENTS.md。
using System;
using UnityEngine;
namespace Regrowth.UI.Art
{
    [CreateAssetMenu(menuName="PAWGATORY/UI/Card visual style")]
    public sealed class CardVisualStyle : ScriptableObject
    {
        [Tooltip("所有卡共用正面框；左右只改CardRoot旋转。")]
        public Sprite frame;
        public Vector2 size = new Vector2(415,614);
        public Vector3 rootAngles = new Vector3(4,0,-4);
        public Vector2 titlePosition = new Vector2(0,-220);
        public Vector2 descriptionPosition = new Vector2(0,-119);
        public float titlePointSize = 24;
        public float descriptionPointSize = 22;
        public Vector2 titleSize = new Vector2(280,58);
        public Vector2 iconPosition = new Vector2(0,45);
        public Vector2 iconSize = new Vector2(280,430.5f);
        public Vector2 descriptionSize = new Vector2(275,160);
        public Vector2 glowPosition = new Vector2(3.5f,31);
        public Vector2 glowSize = new Vector2(538,656);
    }
}
