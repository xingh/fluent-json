﻿// Copyright (c) 2011, Adaptiv Design
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
//     * Redistributions of source code must retain the above copyright notice, this
// list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice,
// this list of conditions and the following disclaimer in the documentation and/or
// other materials provided with the distribution.
//    * Neither the name of the <ORGANIZATION> nor the names of its contributors may
// be used to endorse or promote products derived from this software without specific
// prior written permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
// IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
// INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
// NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
// PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
// POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;

#if !NET20
using System.Linq.Expressions;
#endif

using System.Reflection;

namespace FluentJson.Mapping
{
    abstract public class JsonTypeMappingBase : ICloneable
    {
        internal bool UsesReferencing { get; set; }
        internal Dictionary<string, JsonFieldMappingBase> FieldMappings { get; private set; }

        internal JsonTypeMappingBase()
        {
            this.FieldMappings = new Dictionary<string, JsonFieldMappingBase>();
        }

        abstract public object Clone();
        abstract internal void AutoGenerate();
    }

    public class JsonTypeMapping<T> : JsonTypeMappingBase
    {
        private List<MemberInfo> _exludes;

        public JsonTypeMapping()
        {
            _exludes = new List<MemberInfo>();
        }

        #region ICloneable Members
        public override object Clone()
        {
            JsonTypeMapping<T> clone = new JsonTypeMapping<T>();
            clone.UsesReferencing = this.UsesReferencing;

            Dictionary<string, JsonFieldMappingBase>.Enumerator enumerator = this.FieldMappings.GetEnumerator();
            while (enumerator.MoveNext())
            {
                clone.FieldMappings.Add(enumerator.Current.Key, (JsonFieldMappingBase)enumerator.Current.Value.Clone());
            }

            foreach(MemberInfo exclude in _exludes)
            {
                clone._exludes.Add(exclude);
            }

            return clone;
        }
        #endregion

        /// <summary>
        /// Enables or disables support for referencing multiple occurences to an instance of this type.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public JsonTypeMapping<T> UseReferencing(bool value)
        {
            this.UsesReferencing = value;
            return this;
        }

        /// <summary>
        /// Maps all fields automatically, except any excluded fields.
        /// </summary>
        /// <returns></returns>
        public JsonTypeMapping<T> AllFields()
        {
            List<MemberInfo> members = new List<MemberInfo>();
            members.AddRange(typeof(T).GetFields());
            members.AddRange(typeof(T).GetProperties());

            foreach (MemberInfo memberInfo in members)
            {
                _addFieldMapping(new JsonFieldMapping<object>(memberInfo));
            }

            return this;
        }

        #if !NET20

        /// <summary>
        /// Maps the given field.
        /// </summary>
        /// <param name="fieldExpression"></param>
        /// <returns></returns>
        public JsonTypeMapping<T> Field(Expression<Func<T, object>> fieldExpression)
        {
            MemberInfo memberInfo = _getAccessedMemberInfo(fieldExpression);
            _addFieldMapping(new JsonFieldMapping<object>(memberInfo, memberInfo.Name));

            return this;
        }

        /// <summary>
        /// Maps the given field and allows for specifying a json field name.
        /// </summary>
        /// <param name="fieldExpression"></param>
        /// <param name="jsonObjectField"></param>
        /// <returns></returns>
        public JsonTypeMapping<T> FieldTo(Expression<Func<T, object>> fieldExpression, string jsonObjectField)
        {
            MemberInfo memberInfo = _getAccessedMemberInfo(fieldExpression);
            _addFieldMapping(new JsonFieldMapping<object>(memberInfo, jsonObjectField));

            return this;
        }

        /// <summary>
        /// Maps the given field and allows for custom field mapping expressions.
        /// </summary>
        /// <typeparam name="TField"></typeparam>
        /// <param name="fieldExpression"></param>
        /// <param name="mappingExpression"></param>
        /// <returns></returns>
        public JsonTypeMapping<T> Field<TField>(Expression<Func<T, TField>> fieldExpression, Action<JsonFieldMapping<TField>> mappingExpression)
        {
            MemberInfo memberInfo = _getAccessedMemberInfo<TField>(fieldExpression);

            JsonFieldMapping<TField> fieldMapping = new JsonFieldMapping<TField>(memberInfo);
            mappingExpression(fieldMapping);

            _addFieldMapping(fieldMapping);

            return this;
        }

        /// <summary>
        /// Will prevent the given field from being mapped.
        /// </summary>
        /// <param name="fieldExpression"></param>
        /// <returns></returns>
        public JsonTypeMapping<T> ExceptField(Expression<Func<T, object>> fieldExpression)
        {
            MemberInfo memberInfo = _getAccessedMemberInfo(fieldExpression);

            if (_exludes.Contains(memberInfo))
            {
                throw new Exception("The member '" + memberInfo.Name + "' is already excluded.");
            }

            _exludes.Add(memberInfo);
            
            // See if the excluded member is already mapped, if so remove.
            Dictionary<string, JsonFieldMappingBase>.Enumerator enumerator = this.FieldMappings.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current.Value.ReflectedField.Name == memberInfo.Name)
                {
                    this.FieldMappings.Remove(enumerator.Current.Key);
                    break;
                }
            }

            return this;
        }
        
        private MemberInfo _getAccessedMemberInfo<TField>(Expression<Func<T, TField>> expression)
        {
            Expression current = expression;
            while (current != null)
            {
                if (current.NodeType == ExpressionType.MemberAccess)
                {
                    MemberExpression memberExpression = (MemberExpression)current;
                    if (memberExpression.Member is FieldInfo || memberExpression.Member is PropertyInfo)
                    {
                        return memberExpression.Member;
                    }
                }
                else if (current.NodeType == ExpressionType.Convert)
                {
                    current = (current as UnaryExpression).Operand;
                }
                else if (current.NodeType == ExpressionType.Lambda)
                {
                    current = (current as LambdaExpression).Body;
                }
                else
                {
                    break;
                }
            }

            throw new Exception("This expression does not define a property or field access.");
        }
        #endif

        internal override void AutoGenerate()
        {
            this.AllFields();
        }

        private void _addFieldMapping(JsonFieldMappingBase fieldMapping)
        {
            if (!_exludes.Contains(fieldMapping.ReflectedField))
            {
                if (this.FieldMappings.ContainsKey(fieldMapping.JsonField) && this.FieldMappings[fieldMapping.JsonField].ReflectedField.Name != fieldMapping.ReflectedField.Name)
                {
                    throw new Exception();
                }

                Dictionary<string, JsonFieldMappingBase>.Enumerator enumerator = this.FieldMappings.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Value.ReflectedField.Name == fieldMapping.ReflectedField.Name)
                    {
                        this.FieldMappings.Remove(enumerator.Current.Key);
                        break;
                    }
                }

                this.FieldMappings.Add(fieldMapping.JsonField, fieldMapping);
            }
        }
    }
}