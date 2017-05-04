using UnityEngine;

namespace MYB.MMD4MecanimJitter
{
    public class MMD4M_EyeJitter : MonoBehaviour
    {
        public MMD4MecanimBone leftEye, rightEye;
        public float magnification = 1f;
        public FloatRange interval = new FloatRange(0.04f, 1f, true, false);    //サッカード間隔[sec]   
        public Vector2 range = new Vector2(1f, 4f);                             //interval毎の振動[deg]

        float timer = 0f;

        void Reset()
        {
            interval.min = 0.2f;
            interval.max = 0.5f;
        }

        void Update()
        {
            timer -= Time.deltaTime;

            if (timer < 0f)
            {
                timer = interval.Random();
                var vec = Vector3.zero;
                vec.x = Random.Range(-range.x, range.x) * magnification;
                vec.y = Random.Range(-range.y, range.y) * magnification;

                var rot = Quaternion.Euler(vec);

                leftEye.userRotation = rot;
                rightEye.userRotation = rot;
            }
        }
    }
}