using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public Wallet wallet;

    // 플레이어와 코인이 콜라이더 충돌 감지
    public void OnTriggerEnter(Collider other)
    {
        // 1. 충돌한 물체의 태그(String)를 eCurrency(Enum)로 변환 시도
        // 성공하면 true를 반환하고, 변환된 Enum 값은 'type' 변수에 담깁니다.
        if (System.Enum.TryParse(other.tag, out eCurrency type))
        {
            // 2. 해당 물체에서 Coin 컴포넌트(Value 값) 가져오기
            Coin coin = other.GetComponent<Coin>();

            if (coin != null)
            {
                // 3. 변환된 Enum 타입(type)과 코인의 값(Value)을 지갑에 전달
                wallet.AddCoins(type, coin.Value);

                // 코인 획득 후 비활성화
                other.gameObject.SetActive(false);
            }
        }
        else
        {
            Debug.Log("변환 실패");
        }
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(Vector3.forward * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.S))
        {
            transform.Translate(Vector3.back * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.A))
        {
            transform.Translate(Vector3.left * 2 * Time.deltaTime);
        }
        if(Input.GetKey(KeyCode.D))
        {
            transform.Translate(Vector3.right * 2 * Time.deltaTime);
        }
    }
}
