using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern;

//�ٸ� �������� �ϸŴ����� ���� �� �����ϱ� namespaceó�� 
namespace ShootingScene
{
    //���� ���� ����Ʈ ���  �� -> ������ �̵�
    public class TurnManager : CombinedSingleton<TurnManager>, IGameComponent
    {
        private List<GameObject> playerList = new List<GameObject>();
        // TODO - �� �Ŵ���
        // ���ð����� ���� �־���Ѵ�.
        public void Initialize()
        {
            Debug.Log("[ShootingScene/TurnManager] - ���� ���� TurnManager �ʱ�ȭ");
        }
    }
}
