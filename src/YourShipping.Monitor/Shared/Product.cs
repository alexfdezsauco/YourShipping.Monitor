namespace YourShipping.Monitor.Shared
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    using YourShipping.Monitor.Shared.Annotations;

    public class Product : INotifyPropertyChanged
    {
        private string currency;

        private string department;

        private string departmentCategory;

        private bool hasChanged;

        private int id;

        private bool isAvailable;

        private bool isEnabled;

        private bool isInCart;

        private bool isStored;

        private string name;

        private float price;

        private string store;

        private string url;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Currency
        {
            get => this.currency;
            set
            {
                if (value == this.currency)
                {
                    return;
                }

                this.currency = value;
                this.OnPropertyChanged();
            }
        }

        public string Department
        {
            get => this.department;
            set
            {
                if (value == this.department)
                {
                    return;
                }

                this.department = value;
                this.OnPropertyChanged();
            }
        }

        public string DepartmentCategory
        {
            get => this.departmentCategory;
            set
            {
                if (value == this.departmentCategory)
                {
                    return;
                }

                this.departmentCategory = value;
                this.OnPropertyChanged();
            }
        }

        public bool HasChanged
        {
            get => this.hasChanged;
            set
            {
                if (value == this.hasChanged)
                {
                    return;
                }

                this.hasChanged = value;
                this.OnPropertyChanged();
            }
        }

        public int Id
        {
            get => this.id;
            set
            {
                if (value == this.id)
                {
                    return;
                }

                this.id = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsAvailable
        {
            get => this.isAvailable;
            set
            {
                if (value == this.isAvailable)
                {
                    return;
                }

                this.isAvailable = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => this.isEnabled;
            set
            {
                if (value == this.isEnabled)
                {
                    return;
                }

                this.isEnabled = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsInCart
        {
            get => this.isInCart;
            set
            {
                if (value == this.isInCart)
                {
                    return;
                }

                this.isInCart = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsStored
        {
            get => this.isStored;
            set
            {
                if (value == this.isStored)
                {
                    return;
                }

                this.isStored = value;
                this.OnPropertyChanged();
            }
        }

        public string Name
        {
            get => this.name;
            set
            {
                if (value == this.name)
                {
                    return;
                }

                this.name = value;
                this.OnPropertyChanged();
            }
        }

        public float Price
        {
            get => this.price;
            set
            {
                if (value.Equals(this.price))
                {
                    return;
                }

                this.price = value;
                this.OnPropertyChanged();
            }
        }

        public string Store
        {
            get => this.store;
            set
            {
                if (value == this.store)
                {
                    return;
                }

                this.store = value;
                this.OnPropertyChanged();
            }
        }

        public string Url
        {
            get => this.url;
            set
            {
                if (value == this.url)
                {
                    return;
                }

                this.url = value;
                this.OnPropertyChanged();
            }
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}