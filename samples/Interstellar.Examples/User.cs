﻿namespace Interstellar.Examples;

public class User
{
    public User(Guid id)
    {
        Id = id;
    }

    public Guid Id { get;  }
}