namespace YourShipping.Monitor.Shared
{
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    using YourShipping.Monitor.Shared.Annotations;

    public class Store : INotifyPropertyChanged
    {
        private float categoriesCount;

        private float departmentsCount;

        private bool hasChanged;

        private int id;

        private bool isAvailable;

        private bool isStored;

        private string name;

        private string province;

        private string url;

        public event PropertyChangedEventHandler PropertyChanged;

        public float CategoriesCount
        {
            get => this.categoriesCount;
            set
            {
                if (value.Equals(this.categoriesCount))
                {
                    return;
                }

                this.categoriesCount = value;
                this.OnPropertyChanged();
            }
        }

        public float DepartmentsCount
        {
            get => this.departmentsCount;
            set
            {
                if (value.Equals(this.departmentsCount))
                {
                    return;
                }

                this.departmentsCount = value;
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

        public string Province
        {
            get => this.province;
            set
            {
                if (value == this.province)
                {
                    return;
                }

                this.province = value;
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