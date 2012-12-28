// Copyright (C) 2012 Ferdinand Prantl <prantlf@gmail.com>
// All rights reserved.       
//
// This file is part of SharePosh - SharePoint drive provider for PowerShell.
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Net.Security;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

using Microsoft.IdentityModel.Protocols.WSTrust;

// Web service authentication with Office 365 doesn't work the same way as it does for SharePoint
// on premise. HTTP authentication modes Negotiate, Basic and NTLM are not supported and there is
// no Authentication.asmx web service to perform login. Chris Johnson published a workaround which
// obtains security tokens directly from the Office 365 STS. The code in this file was adopted
// as-is without significant changes.
//
// For more information, see the original article at http://blogs.msdn.com/b/cjohnson/archive/2011/05/14/part-2-headless-authentication-with-sharepoint-online-and-the-client-side-object-model.aspx.

namespace MSDN.Samples.ClaimsAuth
{
    [ServiceContract]
    public interface IWSTrustFeb2005Contract
    {
        [OperationContract(ProtectionLevel = ProtectionLevel.EncryptAndSign, AsyncPattern = true,
            Action = "http://schemas.xmlsoap.org/ws/2005/02/trust/RST/Issue",
            ReplyAction = "http://schemas.xmlsoap.org/ws/2005/02/trust/RSTR/Issue")]
        IAsyncResult BeginIssue(System.ServiceModel.Channels.Message request, AsyncCallback callback, object state);
        System.ServiceModel.Channels.Message EndIssue(IAsyncResult asyncResult);
    }

    public partial class WSTrustFeb2005ContractClient : ClientBase<IWSTrustFeb2005Contract>, IWSTrustFeb2005Contract
    {
        public WSTrustFeb2005ContractClient(Binding binding, EndpointAddress remoteAddress)
            : base(binding, remoteAddress) {}

        public IAsyncResult BeginIssue(Message request, AsyncCallback callback, object state) {
            return base.Channel.BeginIssue(request, callback, state);
        }

        public Message EndIssue(IAsyncResult asyncResult) {
            return base.Channel.EndIssue(asyncResult);
        }
    }

    class RequestBodyWriter : BodyWriter
    {
        WSTrustRequestSerializer _serializer;
        RequestSecurityToken _rst;

        public RequestBodyWriter(WSTrustRequestSerializer serializer, RequestSecurityToken rst)
                    : base(false) {
            if (serializer == null)
                throw new ArgumentNullException("serializer");
            this._serializer = serializer;
            this._rst = rst;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer) {
            _serializer.WriteXml(_rst, writer, new WSTrustSerializationContext());
        }
    }
}
