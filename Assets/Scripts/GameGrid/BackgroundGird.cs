using UnityEngine;
using UnityEngine.UI;

namespace GameGrid
{
    public class BackgroundGird
    {
        private readonly GameObject _obj;
        public GameObject UnityObj => _obj;
        private Image _frontImage;
        private Image _backImage;
        public int x, y;


        public Color deepColor;
        public BackgroundGird(GameObject obj, int x, int y)
        {
            _obj = obj;
            this.x = x;
            this.y = y;
            this.Setup();
        }


        private void Setup()
        {
            _backImage = _obj.GetComponent<Image>();
            _frontImage = _obj.transform.GetChild(0).GetComponent<Image>();
            deepColor = _backImage.color / 1.2f;
            if ((x & 0x1) % 2 == 1 && (y & 0x1) % 2 == 1)
            {
                _frontImage.color /= 1.2f;
            }
        }

        public void UpdateFrontImageColor(Color c, bool must=false)
        {
            _frontImage.color = c;
            if ((x & 0x1) % 2 != 1 || (y & 0x1) % 2 != 1) return;
            if (_frontImage.color == Runtime.Instance.errorColor) return;
            _frontImage.color /= 1.2f;
        }


        public Color FrontColor()
        {
            if ((x & 0x1) % 2 == 1 && (y & 0x1) % 2 == 1 )
            {
                return _frontImage.color * 1.2f;
            }
            return _frontImage.color;
        }
    
    
        public Image FrontImage => _frontImage;
    
    
    }
}