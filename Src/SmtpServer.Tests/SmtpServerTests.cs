﻿using System;
using System.Threading;
using System.Threading.Tasks;
using SmtpServer.Mail;
using SmtpServer.Tests.Mocks;
using Xunit;
using MailKit.Net.Smtp;
using MimeKit;
using SmtpServer.Authentication;

namespace SmtpServer.Tests
{
    public abstract class SmtpServerTest : IDisposable
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        protected SmtpServerTest()
        {
            MessageStore = new MockMessageStore();
            CancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Create an instance of the options builder for the tests.
        /// </summary>
        /// <returns>The options builder to use for the test.</returns>
        public virtual OptionsBuilder CreateOptionsBuilder()
        {
            return new OptionsBuilder()
                .ServerName("localhost")
                .Port(25)
                .MessageStore(MessageStore);
        }

        /// <summary>
        /// Dispose of the resources used by the test.
        /// </summary>
        public virtual void Dispose()
        {
            CancellationTokenSource.Cancel();

            //try
            //{
            //    _smtpServerTask.Wait();
            //}
            //catch (AggregateException e)
            //{
            //    e.Handle(exception => exception is OperationCanceledException);
            //}
        }

        /// <summary>
        /// The message store that is being used to store the messages by default.
        /// </summary>
        public MockMessageStore MessageStore { get; }

        /// <summary>
        /// The cancellation token source for the test.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; }
    }

    public class SmtpServerTests
    {
        readonly MockMessageStore _messageStore;
        readonly OptionsBuilder _optionsBuilder;
        readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public SmtpServerTests()
        {
            _messageStore = new MockMessageStore();

            _optionsBuilder = new OptionsBuilder()
                .ServerName("localhost")
                .Port(25)
                .MessageStore(_messageStore);
        }

        [Fact]
        public void CanReceiveMessage()
        {
            // arrange
            var smtpServer = new SmtpServer(_optionsBuilder.Build());
            var smtpClient = new SmtpClient();
            var smtpServerTask = smtpServer.StartAsync(_cancellationTokenSource.Token);

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MailboxAddress("test1@test.com"));
            message.To.Add(new MailboxAddress("test2@test.com"));
            message.Subject = "Test";
            message.Body = new TextPart("plain")
            {
                Text = "Test Message"
            };

            // act
            smtpClient.Connect("localhost", 25, false);
            smtpClient.Send(message);

            // assert
            Assert.Equal(1, _messageStore.Messages.Count);
            Assert.Equal("test1@test.com", _messageStore.Messages[0].From.AsAddress());
            Assert.Equal(1, _messageStore.Messages[0].To.Count);
            Assert.Equal("test2@test.com", _messageStore.Messages[0].To[0].AsAddress());

            Wait(smtpServerTask);
        }

        [Fact]
        public void CanReceive8BitMimeMessage()
        {
            // arrange
            var smtpServer = new SmtpServer(_optionsBuilder.Build());
            var smtpClient = new SmtpClient();
            var smtpServerTask = smtpServer.StartAsync(_cancellationTokenSource.Token);

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MailboxAddress("test1@test.com"));
            message.To.Add(new MailboxAddress("test2@test.com"));
            message.Subject = "Assunto teste acento çãõáéíóú";
            message.Body = new TextPart("plain")
            {
                Text = "Assunto teste acento çãõáéíóú"
            };
            
            // act
            smtpClient.Connect("localhost", 25, false);
            smtpClient.Send(message);

            // assert
            Assert.Equal(1, _messageStore.Messages.Count);

            Wait(smtpServerTask);
        }

        [Fact]
        public void CanAuthenticateUser()
        {
            // arrange
            string user = null;
            string password = null;
            var userAuthenticator = new DelegatingUserAuthenticator((u, p) =>
            {
                user = u;
                password = p;

                return true;
            });

            var options = _optionsBuilder
                .AllowUnsecureAuthentication()
                .UserAuthenticator(userAuthenticator);

            var smtpServer = new SmtpServer(options.Build());
            var smtpClient = new SmtpClient();
            var smtpServerTask = smtpServer.StartAsync(_cancellationTokenSource.Token);

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MailboxAddress("test1@test.com"));
            message.To.Add(new MailboxAddress("test2@test.com"));
            message.Subject = "Assunto teste acento çãõáéíóú";
            message.Body = new TextPart("plain")
            {
                Text = "Assunto teste acento çãõáéíóú"
            };

            // act
            smtpClient.Connect("localhost", 25, false);
            smtpClient.Authenticate("user", "password");
            smtpClient.Send(message);

            // assert
            Assert.Equal(1, _messageStore.Messages.Count);
            Assert.Equal("user", user);
            Assert.Equal("password", password);

            Wait(smtpServerTask);
        }

        [Fact]
        public void CanReceiveBccInMessageTransaction()
        {
            // arrange
            var smtpServer = new SmtpServer(_optionsBuilder.Build());
            var smtpClient = new SmtpClient();
            var smtpServerTask = smtpServer.StartAsync(_cancellationTokenSource.Token);

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MailboxAddress("test1@test.com"));
            message.To.Add(new MailboxAddress("test2@test.com"));
            message.Cc.Add(new MailboxAddress("test3@test.com"));
            message.Bcc.Add(new MailboxAddress("test4@test.com"));
            message.Subject = "Test";
            message.Body = new TextPart("plain")
            {
                Text = "Test Message"
            };

            // act
            smtpClient.Connect("localhost", 25, false);
            smtpClient.Send(message);

            // assert
            Assert.Equal(1, _messageStore.Messages.Count);
            Assert.Equal("test1@test.com", _messageStore.Messages[0].From.AsAddress());
            Assert.Equal(3, _messageStore.Messages[0].To.Count);
            Assert.Equal("test2@test.com", _messageStore.Messages[0].To[0].AsAddress());
            Assert.Equal("test3@test.com", _messageStore.Messages[0].To[1].AsAddress());
            Assert.Equal("test4@test.com", _messageStore.Messages[0].To[2].AsAddress());

            Wait(smtpServerTask);
        }

        void Wait(Task smtpServerTask)
        {
            _cancellationTokenSource.Cancel();

            try
            {
                smtpServerTask.Wait();
            }
            catch (AggregateException e)
            {
                e.Handle(exception => exception is OperationCanceledException);
            }
        }
    }
}