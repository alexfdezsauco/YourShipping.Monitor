using System.ComponentModel;
using System.Runtime.CompilerServices;
using YourShipping.Monitor.Shared.Annotations;

namespace YourShipping.Monitor.Shared
{
    public class Store : INotifyPropertyChanged
    {
        private bool _hasProductsInCart;
        private int categoriesCount;

        private int departmentsCount;

        private bool hasChanged;

        private int id;

        private bool isAvailable;

        private bool isEnabled;

        private bool isStored;

        private string name;

        private string province;

        private string url;

        public int CategoriesCount
        {
            get => categoriesCount;
            set
            {
                if (value.Equals(categoriesCount))
                {
                    return;
                }

                categoriesCount = value;
                OnPropertyChanged();
            }
        }

        public int DepartmentsCount
        {
            get => departmentsCount;
            set
            {
                if (value.Equals(departmentsCount))
                {
                    return;
                }

                departmentsCount = value;
                OnPropertyChanged();
            }
        }

        public bool HasChanged
        {
            get => hasChanged;
            set
            {
                if (value == hasChanged)
                {
                    return;
                }

                hasChanged = value;
                OnPropertyChanged();
            }
        }

        public int Id
        {
            get => id;
            set
            {
                if (value == id)
                {
                    return;
                }

                id = value;
                OnPropertyChanged();
            }
        }

        public bool IsAvailable
        {
            get => isAvailable;
            set
            {
                if (value == isAvailable)
                {
                    return;
                }

                isAvailable = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => isEnabled;
            set
            {
                if (value == isEnabled)
                {
                    return;
                }

                isEnabled = value;
                OnPropertyChanged();
            }
        }

        public bool IsStored
        {
            get => isStored;
            set
            {
                if (value == isStored)
                {
                    return;
                }

                isStored = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => name;
            set
            {
                if (value == name)
                {
                    return;
                }

                name = value;
                OnPropertyChanged();
            }
        }

        public string Province
        {
            get => province;
            set
            {
                if (value == province)
                {
                    return;
                }

                province = value;
                OnPropertyChanged();
            }
        }

        public string Url
        {
            get => url;
            set
            {
                if (value == url)
                {
                    return;
                }

                url = value;
                OnPropertyChanged();
            }
        }

        public bool HasProductsInCart
        {
            get => _hasProductsInCart;
            set
            {
                if (value == _hasProductsInCart)
                {
                    return;
                }

                _hasProductsInCart = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}