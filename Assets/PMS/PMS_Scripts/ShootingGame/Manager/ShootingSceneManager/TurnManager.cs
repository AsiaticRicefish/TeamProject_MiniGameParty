using UnityEngine;
using DesignPattern;

//�ٸ� �������� �ϸŴ����� ���� �� �����ϱ� namespaceó�� 
namespace ShootingScene_TurnManager
{
    //���� ���� ����Ʈ ���  �� -> ������ �̵�
    public class TurnManager : CombinedSingleton<TurnManager>, IGameComponent
    {
        // TODO - �� �Ŵ���
        // ���ð����� ���� �־���Ѵ�.
        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - ���� ���� TurnManager �ʱ�ȭ");
        }
    }
}
