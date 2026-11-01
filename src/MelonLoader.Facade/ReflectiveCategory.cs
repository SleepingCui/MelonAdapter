using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MelonLoader.Preferences
{
    public class MelonPreferences_ReflectiveCategory
    {
        private readonly Type _type;

        public MelonPreferences_ReflectiveCategory(string identifier, string displayName, Type type)
        {
            Identifier = identifier;
            DisplayName = displayName;
            _type = type;
            Category = MelonPreferences.CreateCategory(identifier, displayName);
            Populate();
        }

        public string Identifier { get; }
        public string DisplayName { get; set; }
        public Type Type => _type;
        public MelonPreferences_Category Category { get; }

        public T GetValue<T>()
        {
            return (T)GetValue(typeof(T));
        }

        public object GetValue(Type type)
        {
            object instance = Activator.CreateInstance(type);
            foreach (MemberInfo member in GetMembers(type))
            {
                MelonPreferences_Entry entry = Category.GetEntry(member.Name);
                if (entry == null)
                {
                    continue;
                }

                object value = entry.BoxedValue;
                PropertyInfo property = member as PropertyInfo;
                if (property != null && property.CanWrite)
                {
                    property.SetValue(instance, value, null);
                    continue;
                }

                FieldInfo field = (FieldInfo)member;
                field.SetValue(instance, value);
            }

            return instance;
        }

        public void LoadFromFile(bool printmsg = true) => Category.LoadFromFile(printmsg);

        public void SaveToFile(bool printmsg = true) => Category.SaveToFile(printmsg);

        public void SetFilePath(string path, bool createIfNotExist = false, bool isSecondary = false) =>
            Category.SetFilePath(path, createIfNotExist, isSecondary);

        public void ResetFilePath() => Category.ResetFilePath();

        public void DestroyFileWatcher() => Category.DestroyFileWatcher();

        private void Populate()
        {
            object instance;
            try
            {
                instance = Activator.CreateInstance(_type);
            }
            catch (Exception exception)
            {
                MelonLogger.Publish(AdapterLogLevel.Error, "Cannot create reflective category '" + Identifier + "' for type '" + _type.FullName + "': " + exception.Message, null, exception);
                return;
            }

            foreach (MemberInfo member in GetMembers(_type))
            {
                Type memberType;
                object defaultValue;
                PropertyInfo property = member as PropertyInfo;
                if (property != null)
                {
                    memberType = property.PropertyType;
                    defaultValue = property.GetValue(instance, null);
                }
                else
                {
                    FieldInfo field = (FieldInfo)member;
                    memberType = field.FieldType;
                    defaultValue = field.GetValue(instance);
                }

                MethodInfo create = typeof(MelonPreferences_Category)
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .First(m => m.Name == "CreateEntry" && m.IsGenericMethodDefinition && m.GetParameters().Length == 4);

                create.MakeGenericMethod(memberType).Invoke(Category, new[] { member.Name, defaultValue, null, (object)false });
            }
        }

        private static IEnumerable<MemberInfo> GetMembers(Type type)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.GetCustomAttributes(typeof(MelonPreferences_DontSaveAttribute), true).Length == 0)
                {
                    yield return field;
                }
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite)
                {
                    continue;
                }

                if (property.GetCustomAttributes(typeof(MelonPreferences_DontSaveAttribute), true).Length > 0)
                {
                    continue;
                }

                yield return property;
            }
        }
    }
}
