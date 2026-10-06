// 节奏表现维护enemy-ai：读取同实体AI朝向，警觉/受击/站定停走路帧；交接docs/handoffs/enemy-ai.handoff。
// 职责：只读敌人生命、运行阶段与速度，播放Dada循环帧和朝向；不写AI、碰撞或伤害。
// 维护：controller/C12；依赖Core/Unity；显式绑定同一敌人及唯一RunController。
// Inspector可调帧序列、FPS与原图朝向；显示大小由纯视觉子节点控制。
// 交接：docs/handoffs/dada-ui-integration.handoff；规范：根AGENTS.md。
using Regrowth.Core;
using Regrowth.Gameplay;
using UnityEngine;

namespace Regrowth.Presentation
{
    [DisallowMultipleComponent]
    public sealed class EnemySpritePresentation : MonoBehaviour
    {
        [SerializeField, Tooltip("本敌人唯一IHealth组件。")]
        private MonoBehaviour healthSource;
        [SerializeField, Tooltip("唯一IRunContext；暂停和选择期间冻结动画。")]
        private MonoBehaviour runSource;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer visual;
        [SerializeField, Tooltip("按原Clip顺序引用Sprite，不复制贴图。")]
        private Sprite[] frames;
        [SerializeField, Min(0.01f)] private float framesPerSecond = 8f;
        [SerializeField, Tooltip("原图是否朝右；只翻转Sprite，不翻转碰撞根节点。")]
        private bool spriteFacesRight = true;

        private EnemyBasicAI ai;
        private IHealth health;
        private IRunContext run;
        private float elapsed;

        private void OnEnable()
        {
            ai = healthSource != null ? healthSource.GetComponent<EnemyBasicAI>() : null;
            health = healthSource as IHealth;
            run = runSource as IRunContext;
            if (health == null || run == null || body == null || visual == null
                || frames == null || frames.Length == 0 || framesPerSecond <= 0f
                || System.Array.Exists(frames, frame => frame == null))
            {
                Debug.LogError("[Enemy Art] Bind health/run/body/renderer and valid animation frames.", this);
                enabled = false;
                return;
            }
            elapsed = 0f;
            visual.sprite = frames[0];
            health.Died += Hide;
        }

        private void LateUpdate()
        {
            if (!health.IsAlive)
            {
                Hide();
                return;
            }
            visual.enabled = true;
            if (!run.IsGameplayActive)
            {
                return;
            }
            bool hasAI = ai != null && ai.isActiveAndEnabled && ai.IsConfigured;
            bool moving = Mathf.Abs(body.linearVelocity.x) > 0.01f;
            if (hasAI)
            {
                // 击退期间沿用朝向，不让速度反向把怪物翻成背对攻击者。
                visual.flipX = (ai.FacingDirection > 0) != spriteFacesRight;
            }
            else if (moving)
            {
                visual.flipX = (body.linearVelocity.x > 0f) != spriteFacesRight;
            }
            if (!moving || (hasAI && (ai.State == EnemyAIState.Alert || ai.IsRecovering)))
            {
                elapsed = 0f;
                visual.sprite = frames[0];
                return;
            }
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, frames.Length / framesPerSecond);
            visual.sprite = frames[Mathf.Min((int)(elapsed * framesPerSecond), frames.Length - 1)];
        }

        private void Hide()
        {
            if (visual != null)
            {
                visual.enabled = false;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= Hide;
            }
            Hide();
        }
    }
}
