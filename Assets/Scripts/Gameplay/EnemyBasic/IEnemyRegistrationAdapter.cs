// Soap/T09：模块内可替换生命周期接点，非Core正式全局注册服务；本轮无强化算法。
// 后续注入的MonoBehaviour实现此接口，可在Register内读取/设置运行快照，使未来生成继承。
namespace Regrowth.Gameplay
{
    public interface IEnemyRegistrationAdapter
    {
        void Register(EnemyBasic enemy);
        void Unregister(EnemyBasic enemy);
    }
}
