﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;

namespace Pathfinder.Core.Authentication
{
    public sealed class AuthenticationServer : IDisposable
    {
        private readonly string _host;
        private readonly int _port;

        private readonly ISimpleSocket _socket;
        private readonly ICharacterParser _characterParser;
        private readonly IGameParser _gameParser;
        private readonly IConnectionTokenParser _connectionTokenParser;
        private readonly IPasswordHashProvider _passwordHashProvider;

        public AuthenticationServer(string host, int port)
        {
            _host = host;
            _port = port;

            _socket = new SimpleSocket();
            _characterParser = new CharacterParser();
            _gameParser = new GameParser();
            _connectionTokenParser = new ConnectionTokenParser();
            _passwordHashProvider = new PasswordHashProvider();
        }

        private void Connect()
        {
            _socket.Connect(_host, _port);
        }

        public bool Authenticate(string account, string password)
        {
            if (String.IsNullOrEmpty(account) || String.IsNullOrEmpty(password))
            {
                return false;
            }

            try
            {
                if (!this._socket.Connected == true)
                {
                    this.Connect();
                }

                var passwordToken = _socket.SendAndReceive("K");

                Debug.WriteLine(string.Format("Echoed passwordToken = {0}", passwordToken));

                var hashedPassword = _passwordHashProvider.Hash(passwordToken, password);

                var message = String.Format("A\t{0}\t{1}", account, hashedPassword);

                var result = _socket.SendAndReceive(message);

                Debug.WriteLine(string.Format("Rec: {0}", result));

                if (result.Contains("KEY"))
                {
                    return true;
                }
            }
            catch (SocketException se)
            {
                Debug.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unexpected exception : {0}", e.ToString());
            }

            return false;
        }

        public IEnumerable<Game> GetGameList()
        {
            var result = _socket.SendAndReceive("M");

            Debug.WriteLine("GameList", result);

            return _gameParser.Parse(result);
        }

        public IEnumerable<Character> GetCharacterList(string gameCode)
        {
            String response;

            response = _socket.SendAndReceive(String.Format("F\t{0}", gameCode));
            //Debug.WriteLine("F: " + response);

            response = _socket.SendAndReceive(String.Format("G\t{0}", gameCode));
            //Debug.WriteLine("G: " + response);

            //response = SendCommand(String.Format("P\t{0}", gameCode));
            //Debug.WriteLine("P: " + response);

            response = _socket.SendAndReceive("C");
            Debug.WriteLine("C: " + response);

            return _characterParser.Parse(response);
        }

        public ConnectionToken ChooseCharacter(string characterId)
        {
            var connectString = String.Format("L\t{0}\tPLAY\r\n", characterId);

            var data = _socket.SendAndReceive(connectString);

            Debug.WriteLine(data);

            return _connectionTokenParser.Parse(data);
        }

        public void Close()
        {
            if (_socket == null)
                return;

            try
            {
                _socket.Close();
            }
            catch (SocketException se)
            {
                Debug.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unexpected exception : {0}", e.ToString());
            }
        }

        public void Dispose()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);

                this.Close();
            }
            catch (SocketException se)
            {
                Debug.WriteLine("SocketException : {0}", se.ToString());
            }
            catch (Exception e)
            {
                Debug.WriteLine("Unexpected exception : {0}", e.ToString());
            }
        }
    }
}