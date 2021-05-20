using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace WcfSelfHostedWithCustomHeaders
{
    class Program
    {
        
        static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

            Uri baseAddress = new Uri(@"http://localhost:8181/hello");

            using (ServiceHost host = new ServiceHost(typeof(HelloWorldService), baseAddress))
            {
                // Enable metadata publishing.
                ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
                smb.HttpGetEnabled = true;
                smb.MetadataExporter.PolicyVersion = PolicyVersion.Policy15;
                host.Description.Behaviors.Add(smb);

                // Open the ServiceHost to start listening for messages. Since
                // no endpoints are explicitly configured, the runtime will create
                // one endpoint per base address for each service contract implemented
                // by the service.
                host.Open();

                Log.Information($"The service is ready at {baseAddress}\nPress <Enter> to stop the service." );
                Console.ReadLine();

                // Close the ServiceHost.
                host.Close();
            }
        }
    }

    [ServiceContract]
    [CustomBehavior]
    public interface IHelloWorldService
    {
        [OperationContract]
        string SayHello(string name);
    }

    
    public class HelloWorldService : IHelloWorldService
    {
        public string SayHello(string name)
        {
            return $"Hello, {name}";
        }
    }

    public class CustomInspectorBehavior : IDispatchMessageInspector
    {
        readonly Dictionary<string, string> _requiredHeaders;
        public CustomInspectorBehavior(Dictionary<string, string> headers)
        {
            _requiredHeaders = headers ?? new Dictionary<string, string>();
        }
        public object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext)
        {
            string displayText = $"Server has received the following message:\nHEADERS:\n{((HttpRequestMessageProperty)request.Properties["httpRequest"])?.Headers}\n{request}\n";
            Log.Information(displayText);
            return null;
        }

        public void BeforeSendReply(ref Message reply, object correlationState)
        {
            if (!reply.Properties.ContainsKey("httpResponse"))
                reply.Properties.Add("httpResponse", new HttpResponseMessageProperty());

            var httpHeader = reply.Properties["httpResponse"] as HttpResponseMessageProperty;
            foreach (var item in _requiredHeaders)
            {
                httpHeader.Headers.Add(item.Key, item.Value);
            }

            string displayText = $"Server has replied the following message:\nHEADERS:\n{httpHeader.Headers}\n{reply}\n";
            Log.Information(displayText);

        }
    }

    public class CustomBehaviorAttribute : Attribute, IContractBehavior, IContractBehaviorAttribute
    {
        public Type TargetContract => typeof(CustomBehaviorAttribute);

        public void AddBindingParameters(ContractDescription contractDescription, ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyClientBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
        }

        public void ApplyDispatchBehavior(ContractDescription contractDescription, ServiceEndpoint endpoint, DispatchRuntime dispatchRuntime)
        {
            var requiredHeaders = new Dictionary<string, string>();

            requiredHeaders.Add("Access-Control-Allow-Origin", "*");
            requiredHeaders.Add("Access-Control-Request-Method", "POST,GET,PUT,DELETE,OPTIONS");
            requiredHeaders.Add("X-Frame-Options", "DENY");
            requiredHeaders.Add("Strict-Transport-Security", "max-age=8640000");
            requiredHeaders.Add("X-Content-Type-Options", "nosniff");
            requiredHeaders.Add("X-Content-Security-Policy", "allow 'self'; default-src 'self';");

            dispatchRuntime.MessageInspectors.Add(new CustomInspectorBehavior(requiredHeaders));
        }

        public void Validate(ContractDescription contractDescription, ServiceEndpoint endpoint)
        {

        }
    }
}