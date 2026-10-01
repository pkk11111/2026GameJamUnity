// 职责：终点绿色门碰触后提交一次胜利；不传送、不收费，不判断精英死亡。
// 维护controller/C14；依赖RunController、PlayerState、Audio.Core/Unity物理。
// 显式绑定唯一玩家/运行阶段；物理回调仅记录接触，LateUpdate复验，保证本帧死亡先处理。
// 交接docs/handoffs/victory-ending.handoff；规范根AGENTS.md。
using Regrowth.Audio;
using UnityEngine;

namespace Regrowth.Runtime
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D)), DefaultExecutionOrder(200)]
    public sealed class VictoryPortal2D : MonoBehaviour
    {
        [SerializeField] private RunController run;
        [SerializeField] private PlayerState player;
        [SerializeField, Tooltip("唯一玩家实体碰撞体，不接受敌人/火球/其他触发器。")]
        private Collider2D playerCollider;
        private bool touching;
        private bool completed;

        private void OnEnable()
        {
            if (run == null || player == null || playerCollider == null
                || !GetComponent<BoxCollider2D>().isTrigger)
            {
                Debug.LogError("[Victory Portal] Bind run/player/collider and enable Is Trigger.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other == playerCollider)
            {
                touching = true;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            OnTriggerEnter2D(other);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == playerCollider)
            {
                touching = false;
            }
        }

        private void LateUpdate()
        {
            if (!completed && touching && playerCollider.enabled && run.TryWin(player))
            {
                completed = true;
                GameAudio.Play(AudioCue.RunWon, player.gameObject);
            }
        }

        private void OnDisable()
        {
            touching = false;
        }
    }
}
