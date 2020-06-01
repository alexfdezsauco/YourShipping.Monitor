namespace YourShipping.Monitor.Shared
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    using YourShipping.Monitor.Shared.Annotations;

    public class Department : INotifyPropertyChanged
    {
        private string category;

        private int id;

        private bool isAvailable;

        private bool isStored;

        private string name;

        private int productsCount;

        private string store;

        private bool hasChanged;

        private bool isEnabled;

        private string url;

        public event PropertyChangedEventHandler PropertyChanged;

        public string Category
        {
            get => this.category;
            set
            {
                if (value == this.category)
                {
                    return;
                }

                this.category = value;
                this.OnPropertyChanged();
            }
        }

        public bool HasChanged
        {
            get => this.hasChanged;
            set
            {
                if (value == this.hasChanged) return;
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

        public int ProductsCount
        {
            get => this.productsCount;
            set
            {
                if (value == this.productsCount)
                {
                    return;
                }

                this.productsCount = value;
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
                if (value == this.url) return;
                this.url = value;
                this.OnPropertyChanged();
            }
        }

        public bool IsEnabled
        {
            get => this.isEnabled;
            set
            {
                if (value == this.isEnabled) return;
                this.isEnabled = value;
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