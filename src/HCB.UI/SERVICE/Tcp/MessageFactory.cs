using System;

namespace HCB.UI
{
    public static class MessageFactory
    {
        /// <summary>
        /// 메시지를 생성합니다.
        /// </summary>
        /// <param name="messageName">HEADER/MESSAGENAME 값</param>
        /// <param name="unitName">HEADER/UNITNAME 값</param>
        /// <param name="content">DATA 내용 (XML 문자열)</param>
        public static Message Create(string messageName, string unitName, string? content = null)
        {
            return new Message
            {
                Header = new MessageHeader
                {
                    MessageName = messageName,
                    UnitName = unitName,
                    Time = DateTime.Now
                },
                Data = new MessageData { Content = content }
            };
        }
    }
}
